namespace ExpenseFlow.Messaging
{
    public static class MsmqNames
    {
        /// <summary>
        /// Private queue path. ".\private$\name" only ever resolves to the
        /// local machine, which is another reason this design is stuck on
        /// one Windows box.
        /// </summary>
        public const string DefaultQueuePath = @".\private$\expenseflow";

        /// <summary>Poison messages are moved here after too many failures.</summary>
        public const string DefaultDeadLetterPath = @".\private$\expenseflow_dead";
    }
}
