
namespace HdbMcp.Server.Domain;

public static class DateBasis
{
    /// Rows before this month are dated by APPROVAL date.
    /// From this month onward they are dated by REGISTRATION date.
    public static readonly DateOnly Cutover = new(2012, 3, 1);

    public static string For(DateOnly month) =>
        month < Cutover ? "approval" : "registration";

    public static bool Crosses(DateOnly from, DateOnly to) =>
        from < Cutover && to >= Cutover;

    public const string Explanation =
        "HDB changed how resale transactions are dated in March 2012. " +
        "Earlier rows are dated by approval date, later rows by registration " +
        "date. A figure spanning the change compares two different things, so " +
        "this tool will not produce one. Request each period separately, or " +
        "restrict the range to one side of March 2012.";
}