using System;
using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models
{
    public class CalendarEvent
    {
        public int Id { get; set; } // Primary Key

        [Required]
        [MaxLength(255)]
        public string Title { get; set; } // Event Title

        [Required]
        public DateTime StartDate { get; set; } // Event Start Date

        [Required]
        public int UserId { get; set; } // ID of the User who created the event
    }
}