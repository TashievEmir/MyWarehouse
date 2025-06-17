using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyWarehouse.Entities
{
    public class BaseEntity
    {
        public long Id { get; set; }
        public long? CreatedById { get; set; }
        [ForeignKey("CreatedById")]
        public User? CreatedBy { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public long? UpdatedById { get; set; }
        [ForeignKey("UpdatedById")]
        public User? UpdatedBy { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }
    }
}
