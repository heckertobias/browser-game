using BrowserGame.Admin.Pages;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace BrowserGame.Admin.Tests;

/// <summary>005 AK-7: is-invalid nur an Feldern, für die ModelState Fehler hat.</summary>
public sealed class BootstrapValidationTagHelperTests
{
    private static readonly EmptyModelMetadataProvider Metadata = new();

    /// <summary>Lässt den Helper auf ein input mit <paramref name="cssClass"/> laufen und liefert das class-Attribut danach.</summary>
    private static string? Process(string field, string? cssClass, ModelStateDictionary modelState, string prefix = "")
    {
        var viewData = new ViewDataDictionary(Metadata, modelState);
        viewData.TemplateInfo.HtmlFieldPrefix = prefix;
        var helper = new BootstrapValidationTagHelper
        {
            For = new ModelExpression(field, new ModelExplorer(Metadata, Metadata.GetMetadataForType(typeof(string)), null)),
            ViewContext = new ViewContext { ViewData = viewData },
        };
        var attributes = new TagHelperAttributeList();
        if (cssClass is not null)
        {
            attributes.Add("class", cssClass);
        }

        var output = new TagHelperOutput("input", attributes, (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));
        helper.Process(new TagHelperContext(new TagHelperAttributeList(), new Dictionary<object, object>(), "test"), output);
        return output.Attributes.TryGetAttribute("class", out var attribute) ? attribute.Value?.ToString() : null;
    }

    private static ModelStateDictionary ErrorAt(string key)
    {
        var state = new ModelStateDictionary();
        state.AddModelError(key, "error");
        return state;
    }

    private static string[] Tokens(string? value) => (value ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);

    [Fact] // 005 AK-7
    public void Field_with_error_gets_is_invalid_and_keeps_its_classes()
    {
        var tokens = Tokens(Process("Input.Host", "form-control", ErrorAt("Input.Host")));

        Assert.Contains("form-control", tokens);
        Assert.Contains("is-invalid", tokens);
        Assert.Equal(2, tokens.Length);
    }

    [Fact] // 005 AK-7
    public void Field_without_error_stays_unmarked()
    {
        Assert.Equal("form-control", Process("Input.Host", "form-control", ErrorAt("Input.Port")));
    }

    [Fact] // 005 AK-7
    public void Valid_model_state_adds_no_class()
    {
        Assert.Null(Process("Input.Host", null, new ModelStateDictionary()));
    }

    [Fact] // 005 AK-7
    public void Is_invalid_is_not_added_twice()
    {
        var tokens = Tokens(Process("Input.Host", "form-control is-invalid", ErrorAt("Input.Host")));

        Assert.Single(tokens, t => t == "is-invalid");
    }

    [Fact] // 005 AK-7
    public void Field_name_includes_the_html_field_prefix()
    {
        Assert.Contains("is-invalid", Tokens(Process("Host", "form-control", ErrorAt("Input.Host"), prefix: "Input")));
    }
}
