using SQLite;

namespace Draftmaster.Data
{
    public enum TransactionType
    {
        RacePurse = 0,
        SponsorIncome = 1,
        DriverSalary = 2,
        StaffSalary = 3,
        Purchase = 4,
        Fee = 5,
        Prize = 6,
        Other = 7
    }

    // Money ledger. One row per cash movement against a career (and optionally a team), so a finance screen can
    // show history and running balance. Written by the sim / economy code; not seeded.
    [Table("Transactions")]
    public class Transaction
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed] public int CareerId { get; set; }
        public int TeamId { get; set; }       // 0 = player-personal, not team-scoped

        public int Season { get; set; }
        public int Week { get; set; }

        public TransactionType Type { get; set; }
        public long Amount { get; set; }       // signed: + income, - expense
        public string Description { get; set; }
    }
}
