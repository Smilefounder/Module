using Kooboo.Data.Context;
using Kooboo.Data.Language;
using Kooboo.Web.Menus;
using System.Collections.Generic;

namespace Sqlite.Menager.Module.MySql
{
    public class MySqlTableMenu : ISideBarMenu
    {
        public SideBarSection Parent => SideBarSection.Database;

        public string Name => "mysql.table";

        public string Icon => "";

        public string Url => "mysql.manager.module/mysql.html";

        public int Order => 5;

        public List<ICmsMenu> SubItems { get; set; }

        public string GetDisplayName(RenderContext Context)
        {
            return Hardcoded.GetValue(Name, Context);
        }
    }
}
