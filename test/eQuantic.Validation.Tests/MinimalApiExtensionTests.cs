using eQuantic.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace eQuantic.Validation.Tests;

[TestFixture]
public sealed class MinimalApiExtensionTests
{
    [Test]
    public void RequireValidation_can_be_added_to_a_route_handler()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddValidation();
        using var app = builder.Build();

        var endpoint = app.MapPost("/orders", (MinimalApiRequest request) => Results.Ok(request));

        Assert.That(() => endpoint.RequireValidation("create"), Throws.Nothing);
    }

    private sealed record MinimalApiRequest(string Number);
}
