using System;
using FluentAssertions;
using Clopay.IDPAdminUI.Core.BusinessLogic.Mappers;
using Clopay.IDPAdminUI.UnitTests.Mocks;
using Xunit;

namespace Clopay.IDPAdminUI.UnitTests.Mappers
{
    [Trait("Category", "Unit")]
    public class PersistedGrantMappers
    {
        [Fact]
        public void CanMapPersistedGrantToModel()
        {
            var persistedGrantKey = Guid.NewGuid().ToString();

            //Generate entity
            var persistedGrant = PersistedGrantMock.GenerateRandomPersistedGrant(persistedGrantKey);

            //Try map to DTO
            var persistedGrantDto = persistedGrant.ToModel();

            //Asert
            persistedGrantDto.Should().NotBeNull();

            persistedGrant.Should().BeEquivalentTo(persistedGrantDto, options => options.Excluding(x => x.SubjectName));
        }
    }
}