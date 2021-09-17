using MonoDevelop.Components.Commands;

namespace LiveXAML
{
  public class SettingsWindowHandler : CommandHandler
  {
    public SettingsWindowHandler() : base()
    {
    }

    protected override void Run()
    {
            LiveXamlProjectExtension live = new LiveXamlProjectExtension();
    }

    protected override void Update(CommandInfo info)
    {
      
    }
  }
}
