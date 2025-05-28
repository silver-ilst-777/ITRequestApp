using System;
using System.IO;

namespace ITRequestApp
{
    public class Request
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Description { get; set; }
        public string Status { get; set; }
        public string FilePath { get; set; }
        public DateTime CreatedAt { get; set; }
        public string UserName { get; set; }
        public string DepartmentName { get; set; }
        public int? AssignedAdminId { get; set; }
        public string AssignedAdminName { get; set; }
        public string CabinetNumber { get; set; }
        public bool IsCompletedByUser { get; set; }

        public string StatusRussian
        {
            get
            {
                return Status switch
                {
                    "Open" => "Открыт",
                    "InProgress" => "Выполняется",
                    "Closed" => "Закрыт",
                    _ => "Неизвестно"
                };
            }
        }
        public string HasFile => !string.IsNullOrEmpty(FilePath) && File.Exists(FilePath) ? "Есть файл" : "Нет файла";
    }
}