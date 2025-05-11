using System;
using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models
{
    public class AssessmentEvent
    {
        public int Id { get; set; } // Primary Key

        [Required]
        [MaxLength(255)]
        public string Title { get; set; } // Assessment Title

        [Required]
        public DateTime Date { get; set; } // Assessment Date

        [Required]
        public int UserId { get; set; } // ID of the User associated with the assessment
    }
}