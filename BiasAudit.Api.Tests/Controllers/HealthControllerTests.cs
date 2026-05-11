using BiasAudit.Api.Controllers;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace BiasAudit.Api.Tests.Controllers;

public class HealthControllerTests
{
    [Fact]
    public void Get_ReturnsOkWithStatus()
    {
        // Arrange
        var controller = new HealthController();

        // Act
        var result = controller.Get();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = okResult.Value;
        Assert.NotNull(response);
        // Możemy sprawdzić właściwości anonimowego obiektu, ale dla prostoty sprawdzimy typ
        Assert.Equal("ok", ((dynamic)response).status);
        Assert.Equal("bias-audit-api", ((dynamic)response).service);
    }
}