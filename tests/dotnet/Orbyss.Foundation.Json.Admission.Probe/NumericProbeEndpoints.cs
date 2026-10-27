using Orbyss.Foundation.Json.Admission.Probe.Contracts;
using Orbyss.Foundation.Json.AspNetCore;

namespace Orbyss.Foundation.Json.Admission.Probe;

/// <summary>Exercises actual registered serializer profiles through HTTP and native OpenAPI.</summary>
internal static class NumericProbeEndpoints
{
    /// <summary>Registers explicit profiles and keeps metadata separate from runtime effect construction.</summary>
    internal static void Register(IServiceCollection services)
    {
        services.AddJsonContract(RequestNumbers.Contract);
        services.AddJsonContract(ResponseNumbers.Contract);
        services.AddJsonContract(StrictNumbers.Request);
        services.AddJsonContract(StrictNumbers.Response);
        services.AddJsonContract(NumericFamily.Contract);
        services.AddJsonContract(NumericSpecialResponse.Contract);
        services.AddJsonContract(NumericNamedRequest.Contract);
        services.AddJsonContract(NumericBase.Contract);
        services.AddJsonContract(CustomNumbers.Contract);
    }

    /// <summary>Publishes read/write semantics using the existing bounded native readers and factories.</summary>
    internal static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapPost("/numeric/request", ReadAsync)
            .WithName("ReadNumbers")
            .WithJsonRequest(RequestNumbers.Contract)
            .Produces(StatusCodes.Status204NoContent);

        endpoints
            .MapGet("/numeric/response", Write)
            .WithName("WriteNumbers")
            .WithJsonResponse(ResponseNumbers.Contract);

        endpoints
            .MapPost("/numeric/strict", EchoAsync)
            .WithName("EchoStrictNumbers")
            .WithJsonRequest(StrictNumbers.Request)
            .WithJsonResponse(StrictNumbers.Response);

        endpoints
            .MapPost("/numeric/family", ReadFamilyAsync)
            .WithJsonRequest(NumericFamily.Contract)
            .Produces(StatusCodes.Status204NoContent);

        endpoints
            .MapPost("/numeric/named", ReadNamedAsync)
            .WithJsonRequest(NumericNamedRequest.Contract)
            .Produces(StatusCodes.Status204NoContent);

        endpoints
            .MapGet("/numeric/special", WriteSpecial)
            .WithJsonResponse(NumericSpecialResponse.Contract);

        endpoints
            .MapGet("/numeric/derived", WriteDerived)
            .WithJsonResponse(NumericBase.Contract);

        endpoints
            .MapGet("/numeric/custom", WriteCustom)
            .WithJsonResponse(CustomNumbers.Contract);
    }

    /// <summary>Admits all values with the configured Foundation request reader.</summary>
    private static async Task<IResult> ReadAsync(HttpContext context, IJsonRequestReader<RequestNumbers> requests)
    {
        _ = await requests.ReadAsync(context, context.RequestAborted);
        return Results.NoContent();
    }

    /// <summary>Emits strings only where WriteAsString requires them.</summary>
    private static IResult Write(IJsonResponseFactory<ResponseNumbers> responses)
    {
        var value = new ResponseNumbers(
            1,
            2,
            3,
            4,
            5,
            6,
            7,
            8
        );

        return responses.Create(value);
    }

    /// <summary>Preserves actual nulls through a same-type strict round trip.</summary>
    private static async Task<IResult> EchoAsync(
        HttpContext context,
        IJsonRequestReader<StrictNumbers> requests,
        IJsonResponseFactory<StrictNumbers> responses
    )
    {
        var value = await requests.ReadAsync(context, context.RequestAborted);
        return responses.Create(value);
    }

    /// <summary>Reads actual permissive numeric forms for the built-in family.</summary>
    private static async Task<IResult> ReadFamilyAsync(HttpContext context, IJsonRequestReader<NumericFamily> requests)
    {
        _ = await requests.ReadAsync(context, context.RequestAborted);
        return Results.NoContent();
    }

    /// <summary>Retains the named-only distinction in actual typed input.</summary>
    private static async Task<IResult> ReadNamedAsync(HttpContext context, IJsonRequestReader<NumericNamedRequest> requests)
    {
        _ = await requests.ReadAsync(context, context.RequestAborted);
        return Results.NoContent();
    }

    /// <summary>Writes the real named and quoted forms through the registered result factory.</summary>
    private static IResult WriteSpecial(IJsonResponseFactory<NumericSpecialResponse> responses)
    {
        var value = new NumericSpecialResponse(
            double.NaN,
            12.5m,
            2.5,
            float.PositiveInfinity
        );
        return responses.Create(value);
    }

    /// <summary>Serializes declared derived metadata through the base response contract.</summary>
    private static IResult WriteDerived(IJsonResponseFactory<NumericBase> responses)
    {
        var value = new NumericDerived(null, 12.5m);
        return responses.Create(value);
    }

    /// <summary>Leaves custom numeric conversion to its explicit profile and schema owner.</summary>
    private static IResult WriteCustom(IJsonResponseFactory<CustomNumbers> responses)
    {
        var value = new CustomNumbers(7, 12);
        return responses.Create(value);
    }
}
