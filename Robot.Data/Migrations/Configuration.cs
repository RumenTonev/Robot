using System;
using System.Data.Entity;
using System.Data.Entity.Migrations;
using System.Linq;

namespace Data.Migrations
{
    public sealed class Configuration : DbMigrationsConfiguration<Robot.Data.RobotContext>
    {
        public Configuration()
        {
            this.AutomaticMigrationsEnabled = true;
            this.AutomaticMigrationDataLossAllowed = true;
        }

        protected override void Seed(Robot.Data.RobotContext context)
        {

        }
    }
}
