using System;

namespace ITRequestApp
{
    public class Review
    {
        public int Id { get; set; }
        public int RequestId { get; set; }
        public int AdminId { get; set; }
        public string Text { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
    }
}