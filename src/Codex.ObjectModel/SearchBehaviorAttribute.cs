namespace Codex.ObjectModel.Attributes
{
    [AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    internal class SearchBehaviorAttribute : Attribute
    {
        public SearchBehaviorAttribute(SearchBehavior behavior)
        {
            Behavior = behavior;
        }

        public SearchBehavior Behavior { get; }
    }
}