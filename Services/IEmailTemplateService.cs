// In a new folder like Services/
namespace BizfreeApp.Services
{
    public interface IEmailTemplateService
    {
        string GetPasswordResetEmailBody(string? userName, string resetLink);
        string GetWelcomeEmailBody(string firstName, string lastName, string userEmail, string initialPassword);
        // ADD THIS METHOD if it's not already there
        string GetPasswordChangedConfirmationEmailBody(string userName);
        string GetProjectCreatedEmailBody(string projectName, string createdByName, string projectDescription, DateTime? startDate, DateTime? endDate);
        string GetProjectDeletedEmailBody(string projectName, string deletedByName, string deletionReason = "");
        string GetTaskCreatedEmailBody(string taskTitle, string createdByName, string? priority, DateOnly? dueDate);
        string GetTaskAssignedEmailBody(string taskTitle, string assignedByName, string? priority, DateOnly? dueDate, string? description);
        string GetProjectMemberAddedEmailBody(string projectName, string addedByName, string? roleName);
    }
}
