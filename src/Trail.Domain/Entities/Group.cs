using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Trail.Domain.Entities
{
    public class Group
    {
        public string GroupName { get; set; }
        public IEnumerable<QuestionAnswerPair> QuestionAnswerPairs { get; set; }
    }

    public class GroupValidator : AbstractValidator<Group>
    {
        public GroupValidator()
        {
            RuleFor(p => p.QuestionAnswerPairs)
                .NotNull()
                .Must(p => p != null && p.Any())
                .WithMessage("At least one question-answer pair is required in each group");

            RuleForEach(p => p.QuestionAnswerPairs)
                .SetValidator(new QuestionAnswerPairValidator());
        }
    }
}

