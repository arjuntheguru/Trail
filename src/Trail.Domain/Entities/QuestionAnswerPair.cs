using FluentValidation;
using System;

namespace Trail.Domain.Entities
{
    public class QuestionAnswerPair
    {
        public string Question { get; set; }
        public string Answer { get; set; }
    }

    public class QuestionAnswerPairValidator : AbstractValidator<QuestionAnswerPair>
    {
        public QuestionAnswerPairValidator()
        {
            RuleFor(p => p.Question).NotEmpty().WithMessage("Question is required");
        }
    }
}

