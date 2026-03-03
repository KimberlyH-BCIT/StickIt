namespace ELKH.ViewModels
{
    public class UserLogVM
    {
        public DateTime LogInTime { get; set; }
        public DateTime? LogOutTime { get; set; }
        public bool Abandoned { get; set; }
    }
}
