using FluentValidation;
using System;

namespace Trail.Domain.Entities
{
    public class Comment
    {
        public string Id { get; set; }
        public string Description { get; set; }
        public string[] Files { get; set; }
        public DateTime CreatedDate { get; set; }
        public string CreatedBy { get; set; }
    }

    public class CommentValidator : AbstractValidator<Comment>
    {
        public CommentValidator()
        {
            // Description is now optional - comment can be empty if images are provided
            RuleFor(p => p.CreatedDate).NotEmpty();
            RuleFor(p => p.CreatedBy).NotEmpty();
        }
    }
}
