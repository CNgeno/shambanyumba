using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RealEstateERP.Core.Common;
using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Models;
using RealEstateERP.Infrastructure.Services;

namespace RealEstateERP.Tests;

public class RentalToolsTests
{
    [Theory]
    [InlineData(BusinessType.Developer, null, false)]   // developers: off by default
    [InlineData(BusinessType.Developer, true, true)]    // ...unless they also let units
    [InlineData(BusinessType.Owner, null, true)]
    [InlineData(BusinessType.Agency, null, true)]
    public void Default_by_business_type(BusinessType type, bool? choice, bool expected)
        => Assert.Equal(expected, RentalTools.Enabled(type, choice));
}
