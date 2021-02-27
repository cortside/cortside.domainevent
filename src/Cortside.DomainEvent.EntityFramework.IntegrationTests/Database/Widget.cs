using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cortside.DomainEvent.EntityFramework.IntegrationTests {
    [Table("Widget")]
    public class Widget {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int WidgetId { get; set; }

        public string Text { get; set; }

        public int Width { get; set; }
        public int Height { get; set; }
    }
}
