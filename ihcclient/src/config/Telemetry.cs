using System.Diagnostics;

namespace Ihc {
    public static class Telemetry
    {
        public static ActivitySource ActivitySource { get; } = new ActivitySource("ihcclient");

        public const string argsTagPrefix = "input.";
        public const string returnValueTag = "retv";

    }

    public static class ActivityExtensions
    {
        /// <summary>
        /// Sets a return value tag on the activity.
        /// </summary>
        /// <typeparam name="T">The type of the return value</typeparam>
        /// <param name="activity">The activity to add the tag to (can be null)</param>
        /// <param name="value">The return value to record</param>
        /// <returns>The activity for method chaining</returns>
        public static Activity SetReturnValue<T>(this Activity activity, T value)
        {
            activity?.SetTag(Telemetry.returnValueTag, value);
            return activity;
        }

        /// <summary>
        /// Sets parameter tags on the activity with names prefixed by "input.".
        /// </summary>
        /// <param name="activity">The activity to add the tags to (can be null)</param>
        /// <param name="parameters">Variable number of named parameter tuples</param>
        /// <returns>The activity for method chaining</returns>
        public static Activity SetParameters(this Activity activity, params (string name, object value)[] parameters)
        {
            if (activity != null)
            {
                foreach (var (name, value) in parameters)
                {
                    activity.SetTag($"{Telemetry.argsTagPrefix}{name}", value);
                }
            }
            return activity;
        }
    }

}


