using Trail.Application.Features.Templates;
using Trail.Domain.Tasks;

namespace Trail.UnitTests.Tasks;

public sealed class TemplateLibraryTests
{
    public static TheoryData<string> Keys => [.. TemplateLibrary.All.Select(t => t.Key)];

    [Theory]
    [MemberData(nameof(Keys))]
    public void Every_library_template_is_a_valid_definition(string key)
    {
        var template = TemplateLibrary.Find(key)!;

        TemplateDefinition.Validate(template.Fields).ShouldBeEmpty();
        template.Name.Length.ShouldBeLessThanOrEqualTo(TaskTemplate.NameMaxLength);
    }

    [Fact]
    public void Library_keys_are_unique()
    {
        TemplateLibrary.All.Select(t => t.Key).ShouldBeUnique();
    }
}
