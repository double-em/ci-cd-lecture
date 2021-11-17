using Microsoft.Extensions.Logging;
using Moq;
using MyApi.Controllers;
using Xunit;

namespace MyApiTest
{
    public class WeatherForecastControllerTest
    {
        [Fact]
        public void ShouldBe_ReturnTrue()
        {

            var logger = new Mock<ILogger<WeatherForecastController>>();
            var _sut = new WeatherForecastController(logger.Object);
            
            Assert.True(_sut.ReturnTrue());
        }
    }
}