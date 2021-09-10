using System.Collections.Generic;

namespace CatalogService.Api.Core.Application.ViewModels
{
    //Sayfada ki itemleri listelerken ki modeli tanımlar. Örn : İlk kez 10 tane ürün listelensin ve aşağı gittikçe 5 er ekle gibi ...
    public class PaginatedItemsViewModel<TEntity> where TEntity:class
    {
        //TEntity: Listelenecek ve modeli oluşturulacak ürün
        public int PageIndex { get; set; } //Hangi sayfadayız
        public int PageSize { get; set; } //Bu sayfada kaç tane ürün listeleniyor
        public long Count { get; set; }//Toplamdaki ürün sayısı
        public IEnumerable<TEntity> Data { get; private set; } //İtemler dışarıdan enumarable bir liste şeklinde ulaşılacak
        public PaginatedItemsViewModel(int pageIndex, int pageSize, long count, IEnumerable<TEntity> data)
        {
            PageIndex = pageIndex;
            PageSize = pageSize;
            Count = count;
            Data = data;
        }
    }
}
