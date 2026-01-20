using Prism.Events;

namespace WareHound.UI.Infrastructure.Events
{
    public class ThemeChangedEvent : PubSubEvent<bool> { } // true = Dark, false = Light
}
