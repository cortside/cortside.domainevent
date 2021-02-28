using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cortside.DomainEvent.EntityFramework.IntegrationTests.Database {
    [Table("Widget")]
    public class Widget {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int WidgetId { get; set; }

        [Required]
        [StringLength(100)]
        public string Text { get; set; }

        [Required]
        public int Width { get; set; }
        [Required]
        public int Height { get; set; }
    }
}
