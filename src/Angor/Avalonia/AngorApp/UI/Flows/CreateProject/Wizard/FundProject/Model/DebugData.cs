namespace AngorApp.UI.Flows.CreateProject.Wizard.FundProject.Model
{
    public class DebugData
    {
        public static string GetDefaultImageUriString(int width, int height)
        {
#if DEBUG
            return $"https://picsum.photos/{width}/{height}";
#else
            return "";            
#endif
        }
    }
}