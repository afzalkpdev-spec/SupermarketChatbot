using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
 
namespace SupermarketBot.Models;
 
[Table("bot_settings")]
public class BotSetting
{
    [Key]
    [Column("setting_key")]
    public string SettingKey { get; set; } = string.Empty;
 
    [Column("setting_value")]
    public bool SettingValue { get; set; }
}