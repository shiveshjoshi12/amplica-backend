namespace BizfreeApp.Constants
{
    public static class Permissions
    {
        // Task Permissions
        public const string TaskRead = "task:read";
        public const string TaskReadAll = "task:read:all";
        public const string TaskCreate = "task:create";
        public const string TaskUpdate = "task:update";
        public const string TaskDelete = "task:delete";
        public const string TaskReadAssigned = "task:read-assigned";

        // Task List Permissions
        public const string TaskListRead = "task-list:read";
        public const string TaskListCreate = "task-list:create";
        public const string TaskListUpdate = "task-list:update";
        public const string TaskListDelete = "task-list:delete";

        // Task Document Permissions
        public const string TaskDocumentCreate = "task-document:create";
        public const string TaskDocumentDelete = "task-document:delete";

        // Task Status Permissions (NEW)
        public const string TaskStatusReadAll = "task-status:read:all";
        public const string TaskStatusCreate = "task-status:create";
        public const string TaskStatusUpdate = "task-status:update";
        public const string TaskStatusDelete = "task-status:delete";

        // Timelog Permissions
        public const string TimelogRead = "timelog:read";
        public const string TimelogReadAll = "timelog:read:all";
        public const string TimelogCreate = "timelog:create";
        public const string TimelogUpdate = "timelog:update";
        public const string TimelogDelete = "timelog:delete";

        // Project Permissions
        public const string ProjectRead = "project:read";
        public const string ProjectReadAll = "project:read:all";
        public const string ProjectCreate = "project:create";
        public const string ProjectUpdate = "project:update";
        public const string ProjectDelete = "project:delete";

        // Project Member Permissions
        public const string ProjectMemberRead = "project-member:read";
        public const string ProjectMemberCreate = "project-member:create";
        public const string ProjectMemberDelete = "project-member:delete";

        // Project Document Permissions
        public const string ProjectDocumentCreate = "project-document:create";
        public const string ProjectDocumentDelete = "project-document:delete";

        // Company Permissions (NEW)
        public const string CompanyRead = "company:read";
        public const string CompanyReadAll = "company:read:all";
        public const string CompanyCreate = "company:create";
        public const string CompanyUpdate = "company:update";
        public const string CompanyDelete = "company:delete";

        // Company Role Permissions (NEW)
        public const string CompanyRoleRead = "companyrole:read";
        public const string CompanyRoleReadAll = "companyrole:read:all";
        public const string CompanyRoleCreate = "companyrole:create";
        public const string CompanyRoleUpdate = "companyrole:update";
        public const string CompanyRoleDelete = "companyrole:delete";

        // Report Permissions (NEW)
        public const string ReportRead = "report:read";
        public const string ReportReadAll = "report:read:all";
        public const string ReportReadDepartment = "report:read:department";
        public const string ReportCreate = "report:create";
        public const string ReportUpdate = "report:update";
        public const string ReportDelete = "report:delete";

        // Employee Permissions (NEW)
        public const string EmployeeRead = "employee:read";
        public const string EmployeeReadAll = "employee:read:all";
        public const string EmployeeCreate = "employee:create";
        public const string EmployeeUpdate = "employee:update";
        public const string EmployeeDelete = "employee:delete";
        /// <summary>Allows a Company Admin to change any employee's password within their company.</summary>
        public const string EmployeeChangePassword = "employee:change-password";
        /// <summary>Allows a Super Admin (CompanyId=0) to change any Company Admin's password.</summary>
        public const string CompanyAdminChangePassword = "company-admin:change-password";

        // Department Permissions (NEW)
        public const string DepartmentRead = "department:read";
        public const string DepartmentReadAll = "department:read:all";
        public const string DepartmentCreate = "department:create";
        public const string DepartmentUpdate = "department:update";
        public const string DepartmentDelete = "department:delete";
    }
}
