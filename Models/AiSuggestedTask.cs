namespace TaskFlow.Models
{
    public class AiSuggestedTask
    {
        public string Title { get; set; } = "";

        public string Description { get; set; } = "";

        public string Priority { get; set; } = "Medium";

        public int EstimatedDays { get; set; }

        public DateTime SuggestedDueDate
        {
            get
            {
                return DateTime.Today.AddDays(EstimatedDays);
            }
        }
    }
}