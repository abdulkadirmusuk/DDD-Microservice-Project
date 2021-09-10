using CatalogService.Api.Core.Domain;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatalogService.Api.Infrastructure.EntityConfigurations
{
    //CatalogBrand nesneni için EFCore configuration işlemi oluşturur
    class CatalogBrandEntiyTypeConfiguration : IEntityTypeConfiguration<CatalogBrand>
    {
        public void Configure(EntityTypeBuilder<CatalogBrand> builder)
        {
            builder.ToTable("CatalogBrand", CatalogContext.DEFAULT_SCHEMA); //catalog ismine sahip bir şema ile. CatalogBrand isimli bir tablo oluşur

            builder.HasKey(ci => ci.Id);

            builder.Property(ci => ci.Id)
                .UseHiLo("catalog_brand_hilo")//unique bir artış için hilo algoritması ile tanım yapılmış
                .IsRequired();

            builder.Property(cb => cb.Brand) //Brand propery si requer bir alan ve max uzunluğu 100 olabilir gibi tanımlar yaptık
                .IsRequired()
                .HasMaxLength(100);
        }
    }
}
