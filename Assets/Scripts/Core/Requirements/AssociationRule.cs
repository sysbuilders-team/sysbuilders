namespace Core.Requirements
{
    public class AssociationRule
    {
        public string SourceCategory { get; }
        public string TargetCategory { get; }
        public int Points { get; }
        public string Reason { get; }
        public string Hint { get; }

        public AssociationRule(string sourceCategory, string targetCategory, int points = 25, string reason = "", string hint = "")
        {
            SourceCategory = sourceCategory;
            TargetCategory = targetCategory;
            Points = points;
            Reason = reason;
            Hint = hint;
        }
    }
}