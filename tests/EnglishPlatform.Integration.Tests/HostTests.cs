using System.Net;
using Shouldly;

namespace EnglishPlatform.Integration.Tests;

public sealed class HostTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Live_and_ready_health_checks_are_healthy()
    {
        factory.SkipIfUnavailable();
        HttpClient client = factory.CreateClient();

        (await client.GetAsync("/health/live", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetAsync("/health/ready", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task OpenApi_document_is_served()
    {
        factory.SkipIfUnavailable();
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Unknown_api_route_returns_problem_details()
    {
        factory.SkipIfUnavailable();
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/api/v1/does-not-exist", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }
}
