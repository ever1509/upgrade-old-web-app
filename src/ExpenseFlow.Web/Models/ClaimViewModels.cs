using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ExpenseFlow.Domain.Entities;

namespace ExpenseFlow.Web.Models
{
    public class ClaimListViewModel
    {
        public IList<ExpenseClaim> Claims { get; set; }
        public string Heading { get; set; }
        public bool ShowClaimant { get; set; }
    }

    public class CreateClaimViewModel
    {
        [Required(ErrorMessage = "Give the claim a title.")]
        [StringLength(200)]
        [Display(Name = "Title")]
        public string Title { get; set; }

        [Display(Name = "Project")]
        public int? ProjectId { get; set; }

        public IList<Project> Projects { get; set; }
    }

    public class ClaimDetailsViewModel
    {
        public ExpenseClaim Claim { get; set; }
        public IList<ExpenseCategory> Categories { get; set; }
        public bool CanEdit { get; set; }
        public bool CanSubmit { get; set; }
        public bool CanDecide { get; set; }
        public bool CanReimburse { get; set; }
        public IList<string> BlockingReasons { get; set; }
        public AddLineViewModel NewLine { get; set; }
    }

    public class AddLineViewModel
    {
        public int ClaimId { get; set; }

        [Required(ErrorMessage = "Pick a category.")]
        [Display(Name = "Category")]
        public int CategoryId { get; set; }

        [Required(ErrorMessage = "Enter the date of the expense.")]
        [DataType(DataType.Date)]
        [Display(Name = "Date")]
        public DateTime ExpenseDate { get; set; }

        [Required(ErrorMessage = "Describe the expense.")]
        [StringLength(300)]
        [Display(Name = "Description")]
        public string Description { get; set; }

        [Required(ErrorMessage = "Enter an amount.")]
        [Range(0.01, 1000000, ErrorMessage = "Amount must be greater than zero.")]
        [Display(Name = "Amount")]
        public decimal Amount { get; set; }

        public AddLineViewModel()
        {
            ExpenseDate = DateTime.UtcNow.Date;
        }
    }

    public class DecisionViewModel
    {
        public int ClaimId { get; set; }

        [StringLength(500)]
        [Display(Name = "Comment")]
        public string Comment { get; set; }
    }
}
