namespace AngorApp.UI.Flows.CreateProject.Wizard.FundProject.Model
{
    public class DebugData
    {
        public static string GetDefaultImageUriString(int width, int height)
        {
#if DEBUG
            var seed = Guid.NewGuid().ToString("N")[..8];
            return $"https://picsum.photos/seed/{seed}/{width}/{height}";
#else
            return "";            
#endif
        }
    }
}