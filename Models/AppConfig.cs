using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SupermarketBot.Models;

[Table("app_config")]
public class AppConfig
{
    [Key]
    [Column("id")]
    public int Id { get; set; } = 1;

    [Column("business_type")]
    public string BusinessType { get; set; } = "grocery"; // "grocery" or "restaurant"
}