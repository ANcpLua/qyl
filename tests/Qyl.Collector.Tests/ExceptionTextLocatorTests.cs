using OpenTelemetry.Proto.Common.V1;
using Qyl.Collector.Ingestion;

namespace Qyl.Collector.Tests;

/// <summary>
/// An XPath locator in exception text is not a URL: it carries no user information, query or fragment to
/// remove, so the sanitizer keeps the text instead of replacing the whole attribute with the failure marker.
/// </summary>
[TestClass]
public sealed class ExceptionTextLocatorTests
{
    private const string SeleniumLocator = "Unable to locate element: //button[text()='Submit']";
    private const string PlaywrightLocator = "waiting for locator('//div[2]')";
    private const string FunctionPredicateLocator = "Timeout: //li[last()] not visible";

    [TestMethod]
    [DataRow(SeleniumLocator)]
    [DataRow(PlaywrightLocator)]
    [DataRow(FunctionPredicateLocator)]
    public void ExceptionTextSanitizer_XPathLocatorInText_KeepsTheText(string message) =>
        Assert.AreEqual(message, ExceptionTextSanitizer.Sanitize("exception.message", new AnyValue { StringValue = message }));
}
