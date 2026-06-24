using System.Collections.Generic;

namespace BLL.DTOs.Student
{
    public class AiSrsRequestDTO
    {
        /// <summary>
        /// Optional list of requirement IDs to base the AI generation on.
        /// If empty/null, ALL requirements for the project will be used.
        /// </summary>
        public List<int>? RequirementIds { get; set; }
    }
}
