using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;

namespace WebProject.Models
{
    // модель описує, як  дані виглядають в Azure Search
    public class MenuItemSearchDocument
    {
        [SimpleField(IsKey = true, IsFilterable = true)]
        public string Id { get; set; }

        [SearchableField(IsSortable = true)]
        public string Name { get; set; }

        [SearchableField]
        public string Description { get; set; }

        [SimpleField(IsFilterable = true, IsSortable = true)]
        public double Price { get; set; }
        
        // пошук за назвою ресторану
        [SearchableField(IsFilterable = true)]
        public string RestaurantName { get; set; }
    }
}