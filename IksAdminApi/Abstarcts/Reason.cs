using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace IksAdminApi;

public abstract class Reason
{
    public string Title {get; set;} = string.Empty; // Причина отображаемая в меню
    public string Text {get; set;} = string.Empty; // Причина отображаемая при бане
    public int MinTime {get; set;} = 0;
    public int MaxTime {get; set;} = 0;
    public int? Duration {get; set;} = null; // Если null то админ выбирает время
    public bool BanOnAllServers {get; set;} = false; // Банить ли по этой причине на всех серверах
    public bool HideFromMenu {get; set;} = false; // Скрыть ли причину из меню(по какой то причине)
    
}