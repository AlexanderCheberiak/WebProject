namespace WebProject.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;


    public class Restaurant
    {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; }

        public string Address { get; set; }

        public string? Description { get; set; }

        public string? PhotoUrl { get; set; }

        public ICollection<MenuItem>? MenuItems { get; set; }
        public ICollection<Order>? Orders { get; set; }
    }


