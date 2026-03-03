using ELKH.Data;
using ELKH.Models;

namespace ELKH.Repositories
{
    public class RegisteredUserLogRepo
    {
        private readonly ApplicationDbContext _context;

        public RegisteredUserLogRepo(ApplicationDbContext context)
        {
            _context = context;
        }

        public IEnumerable<UserLogModel> GetAll()
            => _context.UserLogs
                       .OrderByDescending(l => l.LogInTime)
                       .ToList();

        public UserLogModel? GetById(int id)
            => _context.UserLogs.FirstOrDefault(l => l.PkLogId == id);

        public IEnumerable<UserLogModel> GetByEmail(string email)
            => _context.UserLogs
                       .Where(l => l.FkEmail == email)
                       .OrderByDescending(l => l.LogInTime)
                       .ToList();

        // Last row with LogOutTime == null
        public UserLogModel? GetActiveLog(string email)
            => _context.UserLogs
                       .Where(l => l.FkEmail == email && l.LogOutTime == null)
                       .OrderByDescending(l => l.LogInTime)
                       .FirstOrDefault();

        // Creates new row
        public UserLogModel StartLog(string email)
        {
            var log = new UserLogModel
            {
                FkEmail = email,
                LogInTime = DateTime.UtcNow,
                LogOutTime = null,
                Abandoned = false
            };

            _context.UserLogs.Add(log);
            _context.SaveChanges();

            return log;
        }

        // Sets LogOutTime
        public bool EndLog(int pkLogId)
        {
            var log = GetById(pkLogId);
            if (log is null || log.LogOutTime is not null)
                return false;

            log.LogOutTime = DateTime.UtcNow;
            try
            {
                _context.SaveChanges();
                return true;
            }
            catch
            {
                return false;
            }
        }

        // Close dangling open log and mark as abandoned
        // Policy: If the browser/app closed without logging out, the next successful login will close the previous open log,
        // set LogOutTime to the current UTC time and set Abandoned = true.
        public bool CloseDanglingIfAny(string email)
        {
            var active = GetActiveLog(email);
            if (active is null)
                return false;

            active.LogOutTime = DateTime.UtcNow;
            active.Abandoned = true;
            try
            {
                _context.SaveChanges();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
