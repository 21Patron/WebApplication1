using System;
using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models
{
    public class RecentlyAccessedCourse
    {
        public int Id { get; set; } // Primary Key

        [Required]
        [MaxLength(255)]
        public string CourseTitle { get; set; } // Title of the Course

        [Required]
        public DateTime AccessedDate { get; set; } // Date when the course was last accessed

        [Required]
        public int UserId { get; set; } // ID of the User who accessed the course
    }
}