using System;

/**
 * This file contains extensions to handle dates for all IHC WSDate abstractions.
 * Unfortunately, there is no way to do this once, so we need to repeat the same
 * extension for all ihc namespaces.
 */

namespace Ihc.Soap.Authentication
{
    public partial class WSDate : object {
        public DateTimeOffset ToDateTimeOffset() {
            return new DateTimeOffset(year, monthWithJanuaryAsOne, day, hours, minutes, seconds,  DateHelper.GetWSTimeOffset());
        }
    }
}

namespace Ihc.Soap.Configuration
{
    public partial class WSDate : object {
        public DateTimeOffset ToDateTimeOffset() {
            return new DateTimeOffset(year, monthWithJanuaryAsOne, day, hours, minutes, seconds,  DateHelper.GetWSTimeOffset());
        }
    }
}

namespace Ihc.Soap.Controller
{
    public partial class WSDate : object {
        public DateTimeOffset ToDateTimeOffset() {
            return new DateTimeOffset(year, monthWithJanuaryAsOne, day, hours, minutes, seconds,  DateHelper.GetWSTimeOffset());
        }
    }
}

namespace Ihc.Soap.Module
{
    public partial class WSDate : object {
        public DateTimeOffset ToDateTimeOffset() {
            return new DateTimeOffset(year, monthWithJanuaryAsOne, day, hours, minutes, seconds,  DateHelper.GetWSTimeOffset());
        }
    }
}

namespace Ihc.Soap.Messagecontrollog
{
    public partial class WSDate : object {
        public DateTimeOffset ToDateTimeOffset() {
            return new DateTimeOffset(year, monthWithJanuaryAsOne, day, hours, minutes, seconds,  DateHelper.GetWSTimeOffset());
        }
    }
}

namespace Ihc.Soap.Notificationmanager
{
    public partial class WSDate : object {
        public DateTimeOffset ToDateTimeOffset() {
            return new DateTimeOffset(year, monthWithJanuaryAsOne, day, hours, minutes, seconds,  DateHelper.GetWSTimeOffset());
        }
    }
}

namespace Ihc.Soap.Usermanager
{
    public partial class WSDate : object {
        public DateTimeOffset ToDateTimeOffset() {
            return new DateTimeOffset(year, monthWithJanuaryAsOne, day, hours, minutes, seconds,  DateHelper.GetWSTimeOffset());
        }
    }
}

namespace Ihc.Soap.Openapi
{
    public partial class WSDate : object {
        public DateTimeOffset ToDateTimeOffset() {
            return new DateTimeOffset(year, monthWithJanuaryAsOne, day, hours, minutes, seconds,  DateHelper.GetWSTimeOffset());
        }
    }
}

namespace Ihc.Soap.Timemanager {
    public partial class WSDate : object {
        public DateTimeOffset ToDateTimeOffset() {
            return new DateTimeOffset(year, monthWithJanuaryAsOne, day, hours, minutes, seconds,  DateHelper.GetWSTimeOffset());
        }
    }
}
