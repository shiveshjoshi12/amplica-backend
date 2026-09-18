using Microsoft.AspNetCore.Http;
using BizfreeApp.Data;
using System.IO;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting; // IWebHostEnvironment is in this namespace
using Microsoft.EntityFrameworkCore;
using BizfreeApp.Models; // Assuming your User, ProjectDocument, and TaskDocument models are here
using Microsoft.Extensions.Logging; // Added for logging within the service

namespace BizfreeApp.Services
{
    // Define an interface for dependency injection (good practice)
    public interface IUploadHandler
    {
        Task<string> UploadProfilePhotoAsync(int userId, IFormFile file);
        Task<ProjectDocument> UploadProjectDocumentAsync(int userId, int companyId, int projectId, IFormFile file, string documentName, string? description = null, string? version = null);
        Task<TaskDocument> UploadTaskDocumentAsync(int userId, int companyId, int taskId, IFormFile file, string documentName, string? description = null, string? version = null);
        Task<string> SaveCompanyLogoAsync(IFormFile file);
        Task<CompanyDocument> UploadCompanyDocumentAsync(int userId, int companyId, IFormFile file, string documentName, string? description = null, string? version = null, string? category = null);

    }

    public class UploadHandler : IUploadHandler
    {
        private readonly IWebHostEnvironment _environment;
        private readonly Data.ApplicationDbContext _context;
        private readonly ILogger<UploadHandler> _logger; // Logger injected

        public UploadHandler(IWebHostEnvironment environment, Data.ApplicationDbContext context, ILogger<UploadHandler> logger)
        {
            _environment = environment;
            _context = context;
            _logger = logger; // Initialize logger
        }

        /// <summary>
        /// Saves an IFormFile to the server's file system within a specified folder.
        /// Throws exceptions for file-related errors.
        /// </summary>
        /// <param name="file">The file to save.</param>
        /// <param name="folder">The subfolder within ContentRootPath/Uploads to save the file.</param>
        /// <returns>The relative URL path to the saved file (e.g., "/Uploads/ProfilePictures/unique.jpg").</returns>
        /// <exception cref="ArgumentException">Thrown if the file is null or empty.</exception>
        /// <exception cref="IOException">Thrown for file system errors (e.g., permissions).</exception>
        private async Task<string> SaveFileAsync(IFormFile file, string folder)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("File is empty or null.", nameof(file));

            try
            {
                // Ensure the base uploads directory exists within ContentRootPath
                var baseUploadsFolder = Path.Combine(_environment.ContentRootPath, "Uploads");
                if (!Directory.Exists(baseUploadsFolder))
                {
                    Directory.CreateDirectory(baseUploadsFolder);
                }

                // Create the specific subfolder (e.g., "Uploads/ProfilePictures", "Uploads/Documents")
                var targetFolder = Path.Combine(baseUploadsFolder, folder);
                if (!Directory.Exists(targetFolder))
                {
                    Directory.CreateDirectory(targetFolder);
                }

                // Generate a unique filename to prevent collisions
                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
                var filePath = Path.Combine(targetFolder, fileName);

                _logger.LogInformation($"Attempting to save file to: {filePath}");

                // Save the file to the file system
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                _logger.LogInformation($"File saved successfully to: {filePath}");

                // Construct the relative URL path for the frontend/database
                var relativeUrl = $"/{Path.GetFileName(baseUploadsFolder)}/{folder}/{fileName}";
                return relativeUrl.Replace("\\", "/"); // Ensure forward slashes for URLs
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, $"Access denied when attempting to save file to {folder}. Check folder permissions.");
                throw new IOException($"Permissions error: Cannot write to '{folder}' folder. Please check server folder permissions.", ex);
            }
            catch (DirectoryNotFoundException ex)
            {
                _logger.LogError(ex, $"Directory not found when attempting to save file to {folder}.");
                throw new IOException($"Directory not found for '{folder}'. Check server path configuration.", ex);
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, $"General I/O error when saving file to {folder}.");
                throw new IOException($"A file system error occurred when saving the file to '{folder}'.", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An unexpected error occurred in SaveFileAsync for folder {folder}.");
                throw; // Re-throw any other unexpected exceptions
            }
        }

        /// <summary>
        /// Uploads a profile photo for a user and updates their ProfilePhotoUrl in the database.
        /// Throws InvalidOperationException if user not found.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <param name="file">The profile photo file.</param>
        /// <returns>The URL of the uploaded profile photo.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the user is not found or file save fails.</exception>
        public async Task<string> UploadProfilePhotoAsync(int userId, IFormFile file)
        {
            var url = await SaveFileAsync(file, "Uploads");

            var companyUser = await _context.CompanyUsers.FirstOrDefaultAsync(cu => cu.UserId == userId);
            if (companyUser == null)
            {
                throw new InvalidOperationException("User not found");
            }

            companyUser.ProfilePhotoUrl = url;
            await _context.SaveChangesAsync();

            return url;
        }

        /// <summary>
        /// Uploads a document related to a Project and creates a new ProjectDocument record in the database.
        /// Throws InvalidOperationException for validation errors, DbUpdateException for DB errors, or IOException for file system errors.
        /// </summary>
        /// <param name="userId">The ID of the user uploading the document.</param>
        /// <param name="companyId">The ID of the company the user belongs to.</param>
        /// <param name="projectId">The ID of the project this document is associated with.</param>
        /// <param name="file">The document file (e.g., PDF, Word, Image).</param>
        /// <param name="documentName">The name/title of the document.</param>
        /// <param name="description">An optional description for the document.</param>
        /// <param name="version">An optional version string for the document.</param>
        /// <returns>The newly created ProjectDocument entity.</returns>
        /// <exception cref="InvalidOperationException">Thrown if validation of user or project fails.</exception>
        /// <exception cref="ArgumentException">Thrown if the file is null or empty, or documentName is invalid.</exception>
        /// <exception cref="IOException">Thrown for file system errors.</exception>
        /// <exception cref="DbUpdateException">Thrown for database save errors.</exception>
        public async Task<ProjectDocument> UploadProjectDocumentAsync(int userId, int companyId, int projectId, IFormFile file, string documentName, string? description = null, string? version = null)
        {
            // 1. Validate the uploader user and their company
            var uploaderUser = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId && u.CompanyId == companyId);
            if (uploaderUser == null)
            {
                _logger.LogWarning($"Uploader user ID {userId} not found or not in company {companyId} for project document upload.");
                throw new InvalidOperationException($"Uploader user with ID {userId} not found or does not belong to company {companyId}.");
            }

            // 2. Validate the project and its company association
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.ProjectId == projectId && p.CompanyId == companyId);
            if (project == null)
            {
                _logger.LogWarning($"Project ID {projectId} not found or not in company {companyId} for document upload.");
                throw new InvalidOperationException($"Project with ID {projectId} not found or does not belong to company {companyId}.");
            }

            if (string.IsNullOrWhiteSpace(documentName))
            {
                throw new ArgumentException("Document name cannot be empty for a project document.", nameof(documentName));
            }

            string filePathUrl;
            try
            {
                // 3. Save the file to a specific "ProjectDocuments" folder
                filePathUrl = await SaveFileAsync(file, "ProjectDocuments");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to save project document file to disk for project ID {projectId}.");
                throw; // Re-throw file system errors, let controller handle HTTP status
            }


            // 4. Create a new ProjectDocument record in the database
            var newDocument = new ProjectDocument
            {
                ProjectId = projectId,
                CompanyId = companyId,
                DocumentName = documentName,
                DocumentType = file.ContentType, // Mapped to DocumentType
                FilePath = filePathUrl,           // Mapped to FilePath
                FileSize = file.Length,           // Mapped to FileSize
                Description = description,
                UploadedBy = userId,
                Version = version,
                IsActive = true,
                IsDeleted = false,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow // Initial creation timestamp
            };

            _context.ProjectDocuments.Add(newDocument); // Assuming DbSet<ProjectDocument> exists
            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Project document '{newDocument.DocumentName}' (ID: {newDocument.DocumentId}) saved to DB for Project ID {projectId}.");
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, $"Failed to save ProjectDocument entity to database for Project ID {projectId}.");
                throw; // Re-throw DbUpdateException for the controller to handle
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An unexpected error occurred while saving ProjectDocument to DB for Project ID {projectId}.");
                throw;
            }

            return newDocument;
        }

        /// <summary>
        /// Uploads a document related to a Task and creates a new TaskDocument record in the database.
        /// Throws InvalidOperationException for validation errors, DbUpdateException for DB errors, or IOException for file system errors.
        /// </summary>
        /// <param name="userId">The ID of the user uploading the document.</param>
        /// <param name="companyId">The ID of the company the user belongs to.</param>
        /// <param name="taskId">The ID of the task this document is associated with.</param>
        /// <param name="file">The document file (e.g., PDF, Word, Image).</param>
        /// <param name="documentName">The name/title of the document.</param>
        /// <param name="description">An optional description for the document.</param>
        /// <param name="version">An optional version string for the document.</param>
        /// <returns>The newly created TaskDocument entity.</returns>
        /// <exception cref="InvalidOperationException">Thrown if validation of user or task fails.</exception>
        /// <exception cref="ArgumentException">Thrown if the file is null or empty, or documentName is invalid.</exception>
        /// <exception cref="IOException">Thrown for file system errors.</exception>
        /// <exception cref="DbUpdateException">Thrown for database save errors.</exception>
        public async Task<TaskDocument> UploadTaskDocumentAsync(int userId, int companyId, int taskId, IFormFile file, string documentName, string? description = null, string? version = null)
        {
            // 1. Validate the uploader user and their company
            var uploaderUser = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId && u.CompanyId == companyId);
            if (uploaderUser == null)
            {
                _logger.LogWarning($"Uploader user ID {userId} not found or not in company {companyId} for task document upload.");
                throw new InvalidOperationException($"Uploader user with ID {userId} not found or does not belong to company {companyId}.");
            }

            // 2. Validate the task and its company association
            var task = await _context.Tasks.FirstOrDefaultAsync(t => t.TaskId == taskId && t.CompanyId == companyId);
            if (task == null)
            {
                _logger.LogWarning($"Task ID {taskId} not found or not in company {companyId} for document upload.");
                throw new InvalidOperationException($"Task with ID {taskId} not found or does not belong to company {companyId}.");
            }

            if (string.IsNullOrWhiteSpace(documentName))
            {
                throw new ArgumentException("Document name cannot be empty for a task document.", nameof(documentName));
            }

            string filePathUrl;
            try
            {
                // 3. Save the file to a specific "TaskDocuments" folder
                filePathUrl = await SaveFileAsync(file, "TaskDocuments");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to save task document file to disk for task ID {taskId}.");
                throw; // Re-throw file system errors
            }

            // 4. Create a new TaskDocument record in the database
            var newDocument = new TaskDocument
            {
                TaskId = taskId, // Associate with task
                CompanyId = companyId,
                DocumentName = documentName,
                DocumentType = file.ContentType,
                FilePath = filePathUrl,
                FileSize = file.Length,
                Description = description,
                UploadedBy = userId,
                Version = version,
                IsActive = true,
                IsDeleted = false,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            _context.TaskDocuments.Add(newDocument); // Assuming DbSet<TaskDocument> exists
            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Task document '{newDocument.DocumentName}' (ID: {newDocument.DocumentId}) saved to DB for Task ID {taskId}.");
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, $"Failed to save TaskDocument entity to database for Task ID {taskId}.");
                throw; // Re-throw DbUpdateException for the controller to handle
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An unexpected error occurred while saving TaskDocument to DB for Task ID {taskId}.");
                throw;
            }

            return newDocument;
        }
    public async Task<string> SaveCompanyLogoAsync(IFormFile file)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("Logo file is empty or null.", nameof(file));

            // Validate that it's an image file
            var allowedContentTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif", "image/bmp" };
            if (!allowedContentTypes.Contains(file.ContentType.ToLowerInvariant()))
            {
                throw new ArgumentException("File must be an image (JPEG, PNG, GIF, or BMP).", nameof(file));
            }

            try
            {
                // Use the existing SaveFileAsync method with a "CompanyLogos" folder
                var logoUrl = await SaveFileAsync(file, "CompanyLogos");
                _logger.LogInformation($"Company logo saved successfully: {logoUrl}");
                return logoUrl;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save company logo file.");
                throw;
            }
            }

        public async Task<CompanyDocument> UploadCompanyDocumentAsync(int userId, int companyId, IFormFile file, string documentName, string? description = null, string? version = null, string? category = null)
        {
            // 1. Validate the uploader user and their company
            var uploaderUser = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId && u.CompanyId == companyId);
            if (uploaderUser == null)
            {
                _logger.LogWarning($"Uploader user ID {userId} not found or not in company {companyId} for company document upload.");
                throw new InvalidOperationException($"Uploader user with ID {userId} not found or does not belong to company {companyId}.");
            }

            // 2. Validate the company exists
            var company = await _context.Companies.FirstOrDefaultAsync(c => c.CompanyId == companyId);
            if (company == null)
            {
                _logger.LogWarning($"Company ID {companyId} not found for document upload.");
                throw new InvalidOperationException($"Company with ID {companyId} not found.");
            }

            if (string.IsNullOrWhiteSpace(documentName))
            {
                throw new ArgumentException("Document name cannot be empty for a company document.", nameof(documentName));
            }

            string filePathUrl;
            try
            {
                // 3. Save the file to a specific "CompanyDocuments" folder
                filePathUrl = await SaveFileAsync(file, "CompanyDocuments");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to save company document file to disk for company ID {companyId}.");
                throw; // Re-throw file system errors
            }

            // 4. Determine document type from file extension
            var documentType = Path.GetExtension(file.FileName).TrimStart('.').ToUpperInvariant();

            // 5. Create a new CompanyDocument record in the database
            var newDocument = new CompanyDocument
            {
                CompanyId = companyId,
                DocumentName = documentName,
                DocumentType = documentType,
                DocumentUrl = filePathUrl,
                FileSize = file.Length,
                Description = description,
                Version = version,
                Category = category,
                IsActive = true,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedBy = userId,
                UpdatedBy = userId
            };

            _context.CompanyDocuments.Add(newDocument);

            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Company document '{newDocument.DocumentName}' (ID: {newDocument.Id}) saved to DB for Company ID {companyId}.");
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, $"Failed to save CompanyDocument entity to database for Company ID {companyId}.");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An unexpected error occurred while saving CompanyDocument to DB for Company ID {companyId}.");
                throw;
            }

            return newDocument;
        }
    }
    }
