using DAL.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Data
{
	public partial class PostgreContext : DbContext
	{
		static PostgreContext()
		{
			Npgsql.NpgsqlConnection.GlobalTypeMapper.MapEnum<UserRole>("user_role");
			Npgsql.NpgsqlConnection.GlobalTypeMapper.MapEnum<UserStatus>("user_status");
		}
	}
}
