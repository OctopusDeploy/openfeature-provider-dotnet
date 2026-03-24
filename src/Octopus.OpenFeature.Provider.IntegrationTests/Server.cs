using WireMock;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using WireMock.Types;
using WireMock.Util;

namespace Octopus.OpenFeature.Provider.IntegrationTests;

public class Server : IDisposable
{
    readonly WireMockServer _server;
    readonly Dictionary<string, string> _responses = new();
    public string Url => _server.Url!;

    public Server()
    {
        _server = WireMockServer.Start();
        _server
            .Given(Request.Create().WithPath("/api/featuretoggles/v3/").UsingGet())
            .RespondWith(Response.Create()
                .WithTransformer()
                .WithCallback(req =>
                {
                    var authHeader = req.Headers?["Authorization"]?.FirstOrDefault();
                    if (authHeader != null && authHeader.StartsWith("Bearer "))
                    {
                        var token = authHeader["Bearer ".Length..];
                        if (_responses.TryGetValue(token, out var responseBody))
                        {
                            return new ResponseMessage
                            {
                                StatusCode = 200,
                                Headers = new Dictionary<string, WireMock.Types.WireMockList<string>>
                                {
                                    ["Content-Type"] = new WireMock.Types.WireMockList<string>("application/json"),
                                    ["ContentHash"] = new WireMock.Types.WireMockList<string>(Convert.ToBase64String([0x01]))
                                },
                                BodyData = new BodyData
                                {
                                    BodyAsString = responseBody,
                                    DetectedBodyType = BodyType.String
                                }
                            };
                        }
                    }

                    return new ResponseMessage { StatusCode = 401 };
                }));
    }

    public string Configure(string json)
    {
        var token = Guid.NewGuid().ToString();
        _responses[token] = json;
        return token;
    }

    public void Dispose()
    {
        _server.Dispose();
    }
}
