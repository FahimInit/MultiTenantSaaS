namespace SaaS.Domain.Subscriptions;

public enum BillingCycle
{
    Monthly = 1,
    Annual = 2
}

public enum SubscriptionStatus
{
    Trialing = 1,
    Active = 2,
    PastDue = 3,
    Canceled = 4,
    Expired = 5
}

public enum SubscriptionEventType
{
    TrialStarted = 1,
    Subscribed = 2,
    Upgraded = 3,
    Downgraded = 4,
    Canceled = 5,
    Renewed = 6,
    Expired = 7
}
