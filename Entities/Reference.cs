using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyWarehouse.Entities
{
    public class Reference : BaseEntity
    {
        public string NameRu { get; set; }
        public string NameKg { get; set; }
        public string Code { get; set; }
        public long? ReferenceParentId { get; set; }
        public Reference ReferenceParent { get; set; }
    }
}
