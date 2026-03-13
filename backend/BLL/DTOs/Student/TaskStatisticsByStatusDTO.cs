namespace BLL.DTOs.Student
{
    /// <summary>
    /// DTO for task statistics grouped by status
    /// </summary>
    public class TaskStatisticsByStatusDTO
    {
        public int TodoTasks { get; set; }
        public int InProgressTasks { get; set; }
        public int DoneTasks { get; set; }
        public int TotalTasks { get; set; }
    }
}