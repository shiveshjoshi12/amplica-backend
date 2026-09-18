// In Services/
using static System.Net.WebRequestMethods;

namespace BizfreeApp.Services
{
    public class EmailTemplateService : IEmailTemplateService
    {
        public string GetPasswordResetEmailBody(string userName, string resetLink)
        {
            // The template from your ForgotPassword API, now centralized.
            string greeting = string.IsNullOrEmpty(userName) ? "Hello," : $"Hello {userName},";

            return $@"
            <html>
            <head>
                <style>
                    body {{ font-family: Arial, sans-serif; background-color: #f5f5f5; padding: 20px; color: #333; }}
                    .container {{ max-width: 600px; margin: auto; background: #fff; padding: 30px; border-radius: 8px; box-shadow: 0 4px 8px rgba(0,0,0,0.1); }}
                    h2 {{ color: #0056b3; }}
                    p {{ line-height: 1.6; }}
                    .btn {{ display: inline-block; padding: 12px 25px; margin: 20px 0; background-color: #007BFF; color: white !important; text-decoration: none; border-radius: 5px; font-weight: bold; }}
                    .footer {{ margin-top: 20px; font-size: 0.9em; color: #777; }}
                </style>
            </head>
            <body>
                <div class='container'>
                    <h2>Password Reset Request</h2>
                    <p>{greeting}</p>
                    <p>We received a request to reset your password for your Amplica account. Click the button below to set a new password:</p>
                    <a href='{resetLink}' class='btn'>Reset Password</a>
                    <p>If you did not request a password reset, you can safely ignore this email. The link will expire in 1 hour.</p>
                    <p class='footer'>Thanks,<br/>The Amplica Team</p>
                </div>
            </body>
            </html>";
        }

        public string GetPasswordChangedConfirmationEmailBody(string userName)
        {
            return $@"
            <html>
            <head>
                <style>
                    body {{ font-family: Arial, sans-serif; background-color: #f5f5f5; padding: 20px; color: #333; }}
                    .container {{ max-width: 600px; margin: auto; background: #fff; padding: 30px; border-radius: 8px; box-shadow: 0 4px 8px rgba(0,0,0,0.1); }}
                    h2 {{ color: #28a745; }}
                </style>
            </head>
            <body>
                <div class='container'>
                    <h2>Password Changed Successfully</h2>
                    <p>Hello {userName},</p>
                    <p>This email is to confirm that the password for your Amplica account has been successfully changed.</p>
                    <p>If you did not make this change, please contact our support team immediately.</p>
                </div>
            </body>
            </html>";
        }

        public string GetWelcomeEmailBody(string firstName, string lastName, string userEmail, string initialPassword)
        {
            string name = (!string.IsNullOrWhiteSpace(firstName) || !string.IsNullOrWhiteSpace(lastName))
                ? $"{firstName} {lastName}".Trim()
                : "User";

            //// Replace with your actual login URL
            //string loginUrl = "https://yourdomain.com/login";

            return $@"
    <html>
    <head>
        <style>
            body {{ font-family: Arial, sans-serif; background-color: #f5f5f5; padding: 20px; color: #333; }}
            .container {{ max-width: 600px; margin: auto; background: #fff; padding: 30px; border-radius: 8px; box-shadow: 0 4px 8px rgba(0,0,0,0.1); }}
            h2 {{ color: #0056b3; }}
            p {{ line-height: 1.6; }}
            .credentials {{ background-color: #f9f9f9; border-left: 4px solid #007BFF; padding: 15px; margin: 20px 0; }}
            .btn {{
                display: inline-block;
                padding: 12px 20px;
                margin: 20px 0;
                font-size: 16px;
                color: #fff !important;
                background-color: #007BFF;
                border-radius: 5px;
                text-decoration: none;
            }}
            .btn:hover {{
                background-color: #0056b3;
            }}
            .footer {{ margin-top: 20px; font-size: 0.9em; color: #777; }}
        </style>
    </head>
    <body>
        <div class='container'>
            <h2>Welcome to Amplica!</h2>
            <p>Dear {name},</p>
            <p>Welcome aboard! Your account has been successfully created and you can now log in to the Amplica platform.</p>
            <div class='credentials'>
                <p><strong>Login Email:</strong> {userEmail}</p>
                <p><strong>Temporary Password:</strong> <strong>{initialPassword}</strong></p>
            </div>
            <p>For your security, please log in and change your password at your earliest convenience.</p>
            
            <a href='https://www.amplica.in/' class='btn'>Go to Login</a>

            <p class='footer'>Thank you,<br/>The Amplica Team</p>
        </div>
    </body>
    </html>";
        }

        public string GetProjectCreatedEmailBody(string projectName, string createdByName, string projectDescription, DateTime? startDate, DateTime? endDate)
        {
            string dateInfo = "";
            if (startDate.HasValue && endDate.HasValue)
            {
                dateInfo = $"<p><strong>Project Timeline:</strong> {startDate.Value:MMM dd, yyyy} - {endDate.Value:MMM dd, yyyy}</p>";
            }
            else if (startDate.HasValue)
            {
                dateInfo = $"<p><strong>Start Date:</strong> {startDate.Value:MMM dd, yyyy}</p>";
            }
            else if (endDate.HasValue)
            {
                dateInfo = $"<p><strong>End Date:</strong> {endDate.Value:MMM dd, yyyy}</p>";
            }

            return $@"
                <!DOCTYPE html>
                <html>
                <head>
                    <meta charset='utf-8'>
                    <title>New Project Created</title>
                    <style>
                        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                        .header {{ background-color: #4CAF50; color: white; padding: 20px; text-align: center; border-radius: 5px 5px 0 0; }}
                        .content {{ background-color: #f9f9f9; padding: 20px; border-radius: 0 0 5px 5px; }}
                        .project-info {{ background-color: white; padding: 15px; border-radius: 5px; margin: 15px 0; }}
                        .footer {{ text-align: center; margin-top: 20px; font-size: 12px; color: #666; }}
                        .btn {{ display: inline-block; padding: 10px 20px; background-color: #4CAF50; color: white; text-decoration: none; border-radius: 5px; margin: 10px 0; }}
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <div class='header'>
                            <h1>🚀 New Project Created!</h1>
                        </div>
                        <div class='content'>
                            <p>Hello,</p>
                            <p>A new project has been created in your organization by <strong>{createdByName}</strong>.</p>
                            
                            <div class='project-info'>
                                <h3>📋 Project Details</h3>
                                <p><strong>Project Name:</strong> {projectName}</p>
                                {(string.IsNullOrEmpty(projectDescription) ? "" : $"<p><strong>Description:</strong> {projectDescription}</p>")}
                                {dateInfo}
                                <p><strong>Created By:</strong> {createdByName}</p>
                                //<p><strong>Created On:</strong> {DateTime.UtcNow:MMM dd, yyyy 'at' hh:mm tt} UTC</p>
                            </div>

                            <p>You can now access this project through your dashboard and start collaborating with your team.</p>
                            
                            <p>If you have any questions about this project, please reach out to the project creator or your project manager.</p>
                        </div>
                        <div class='footer'>
                            <p>This is an automated notification from Amplica.</p>
                            <p>© 2025 Amplica. All rights reserved.</p>
                        </div>
                    </div>
                </body>
                </html>";
        }

        public string GetProjectDeletedEmailBody(string projectName, string deletedByName, string deletionReason = "")
        {
            string reasonSection = string.IsNullOrEmpty(deletionReason) ? "" :
                $"<p><strong>Reason:</strong> {deletionReason}</p>";

            return $@"
                <!DOCTYPE html>
                <html>
                <head>
                    <meta charset='utf-8'>
                    <title>Project Deleted</title>
                    <style>
                        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                        .header {{ background-color: #f44336; color: white; padding: 20px; text-align: center; border-radius: 5px 5px 0 0; }}
                        .content {{ background-color: #f9f9f9; padding: 20px; border-radius: 0 0 5px 5px; }}
                        .project-info {{ background-color: white; padding: 15px; border-radius: 5px; margin: 15px 0; border-left: 4px solid #f44336; }}
                        .footer {{ text-align: center; margin-top: 20px; font-size: 12px; color: #666; }}
                        .warning {{ background-color: #fff3cd; border: 1px solid #ffeaa7; padding: 10px; border-radius: 5px; margin: 15px 0; }}
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <div class='header'>
                            <h1>🗑️ Project Deleted</h1>
                        </div>
                        <div class='content'>
                            <p>Hello,</p>
                            <p>We're writing to inform you that a project has been deleted from your organization.</p>
                            
                            <div class='project-info'>
                                <h3>📋 Deleted Project Details</h3>
                                <p><strong>Project Name:</strong> {projectName}</p>
                                <p><strong>Deleted By:</strong> {deletedByName}</p>
                                <p><strong>Deleted On:</strong> {DateTime.UtcNow:MMM dd, yyyy 'at' hh:mm tt} UTC</p>
                                {reasonSection}
                            </div>

                            <div class='warning'>
                                <p><strong>⚠️ Important Notice:</strong></p>
                                <p>This project has been soft-deleted and may be recoverable by your administrator. All associated tasks, documents, and member assignments have been preserved.</p>
                            </div>

                            <p>If you believe this deletion was made in error or if you need to recover any project data, please contact your project administrator or system administrator immediately.</p>
                            
                            <p>For any questions or concerns, please reach out to your project management team.</p>
                        </div>
                        <div class='footer'>
                            <p>This is an automated notification from Amplica.</p>
                            <p>© 2025 Amplica. All rights reserved.</p>
                        </div>
                    </div>
                </body>
                </html>";
        }

        public string GetTaskCreatedEmailBody(string taskTitle, string createdByName, string? priority, DateOnly? dueDate)
        {
            string dateInfo = dueDate.HasValue ? $"<p><strong>Due Date:</strong> {dueDate.Value:MMM dd, yyyy}</p>" : "";
            string priorityInfo = !string.IsNullOrEmpty(priority) ? $"<p><strong>Priority:</strong> {priority}</p>" : "";

            return $@"
    <html>
    <head>
        <style>
            body {{ font-family: Arial, sans-serif; background-color: #f5f5f5; padding: 20px; color: #333; }}
            .container {{ max-width: 600px; margin: auto; background: #fff; padding: 30px; border-radius: 8px; border-top: 5px solid #007BFF; }}
            h2 {{ color: #0056b3; }}
            .task-info {{ background-color: #f9f9f9; padding: 15px; border-radius: 5px; margin: 20px 0; border-left: 4px solid #007BFF; }}
            .footer {{ margin-top: 20px; font-size: 0.9em; color: #777; }}
        </style>
    </head>
    <body>
        <div class='container'>
            <h2>📝 New Task Created</h2>
            <p>Hello Team,</p>
            <p>A new task has been added to the project by <strong>{createdByName}</strong>.</p>
            
            <div class='task-info'>
                <p><strong>Task:</strong> {taskTitle}</p>
                {priorityInfo}
                {dateInfo}
            </div>

            <p>You can view and manage this task on the Amplica dashboard.</p>
            <a href='https://www.amplica.in/' style='display: inline-block; padding: 10px 20px; background-color: #007BFF; color: white; text-decoration: none; border-radius: 5px;'>View Dashboard</a>
            
            <p class='footer'>This is an automated notification from Amplica.</p>
        </div>
    </body>
    </html>";
        }

        public string GetTaskAssignedEmailBody(string taskTitle, string assignedByName, string? priority, DateOnly? dueDate, string? description)
        {
            string dateInfo = dueDate.HasValue ? $"<p><strong>Due Date:</strong> {dueDate.Value:MMM dd, yyyy}</p>" : "";
            string priorityInfo = !string.IsNullOrEmpty(priority) ? $"<p><strong>Priority:</strong> {priority}</p>" : "";
            string descInfo = !string.IsNullOrEmpty(description) ? $"<p><strong>Description:</strong> {description}</p>" : "";

            return $@"
    <html>
    <head>
        <style>
            body {{ font-family: Arial, sans-serif; background-color: #f5f5f5; padding: 20px; color: #333; }}
            .container {{ max-width: 600px; margin: auto; background: #fff; padding: 30px; border-radius: 8px; border-top: 5px solid #28a745; }}
            h2 {{ color: #28a745; }}
            .task-card {{ background-color: #ffffff; border: 1px solid #e0e0e0; padding: 20px; border-radius: 8px; margin: 20px 0; }}
            .footer {{ margin-top: 20px; font-size: 0.9em; color: #777; }}
        </style>
    </head>
    <body>
        <div class='container'>
            <h2>🎯 New Task Assigned to You</h2>
            <p>Hello,</p>
            <p>You have been assigned a new task by <strong>{assignedByName}</strong>.</p>
            
            <div class='task-card'>
                <h3 style='margin-top:0;'>{taskTitle}</h3>
                {descInfo}
                <hr style='border: 0; border-top: 1px solid #eee;' />
                {priorityInfo}
                {dateInfo}
            </div>

            <p>Please log in to your account to start working on this task.</p>
            <a href='https://www.amplica.in/' style='display: inline-block; padding: 10px 20px; background-color: #28a745; color: white; text-decoration: none; border-radius: 5px;'>Open Task in Amplica</a>

            <p class='footer'>Thank you,<br/>The Amplica Team</p>
        </div>
    </body>
    </html>";
        }

        public string GetProjectMemberAddedEmailBody(string projectName, string addedByName, string? roleName)
        {
            string roleInfo = !string.IsNullOrEmpty(roleName) ? $"<p><strong>Your Role:</strong> {roleName}</p>" : "";

            return $@"
    <html>
    <head>
        <style>
            body {{ font-family: Arial, sans-serif; background-color: #f5f5f5; padding: 20px; color: #333; }}
            .container {{ max-width: 600px; margin: auto; background: #fff; padding: 30px; border-radius: 8px; border-top: 5px solid #28a745; }}
            h2 {{ color: #28a745; }}
            .info-box {{ background-color: #f9f9f9; padding: 15px; border-radius: 5px; margin: 20px 0; border-left: 4px solid #28a745; }}
            .footer {{ margin-top: 20px; font-size: 0.9em; color: #777; }}
        </style>
    </head>
    <body>
        <div class='container'>
            <h2>🤝 Added to Project</h2>
            <p>Hello,</p>
            <p>You have been added to the project <strong>{projectName}</strong> by <strong>{addedByName}</strong>.</p>
            
            //<div class='info-box'>
            //    <p><strong>Project:</strong> {projectName}</p>
            //    {roleInfo}
            //</div>

            <p>You can now collaborate with your team and manage project tasks through your dashboard.</p>
            <a href='https://www.amplica.in/' style='display: inline-block; padding: 10px 20px; background-color: #28a745; color: white; text-decoration: none; border-radius: 5px;'>View Project Dashboard</a>
            
            <p class='footer'>This is an automated notification from Amplica.</p>
        </div>
    </body>
    </html>";
        }
    }
}
