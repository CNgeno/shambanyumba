using RealEstateERP.Core.Models;

namespace RealEstateERP.Booking.Web.Services;

/// <summary>One step of "Get started as a host".</summary>
public sealed record SetupStep(int Number, bool Done, string Title, string Text, string Href, string Action, string? ReviewHref);

/// <summary>
/// The six host setup steps, in order. Each unlocks when the one before is done (units need a property; photos and
/// rates need units; publishing needs rates). Used by the Get started page and the "Getting started" bar.
/// </summary>
public static class SetupSteps
{
    public const string Cookie = "sn_setup";

    public static IReadOnlyList<SetupStep> For(HostSetupProgress p) =>
    [
        new(1, p.HasProperty, "Add your property", "The guest house, apartment block or estate where your stays are.", "/host/properties/new", "Add property", "/host/properties"),
        new(2, p.HasUnits, "Add the units", "Each room or flat guests can book, with its nightly rate. One at a time or many at once.", "/host/properties", "Add units", "/host/properties"),
        new(3, p.HasPhotos, "Add photos", "The rooms guests will use, plus the outside and compound.", "/host/properties", "Add photos", "/host/properties"),
        new(4, p.AllRated, "Check rates and rules", "Weekend and holiday rates, discounts for longer stays, check-in times and house rules.", "/host/stays", "Rates and rules", "/host/stays"),
        new(5, p.AnyPublished, "Publish", "Published stays appear on the guest site and can be booked.", "/host/stays", "Publish stays", "/host/stays"),
        new(6, p.HasPaymentInstructions, "Tell guests how to pay", "Your M-PESA paybill or till (and bank), shown on every confirmation.", "/host/start#pay", "Set it now", "/host/start#pay")
    ];

    /// <summary>The step to do now: the first one not done (null when all are done).</summary>
    public static SetupStep? Current(IReadOnlyList<SetupStep> steps)
    {
        foreach (var s in steps) if (!s.Done) return s;
        return null;
    }
}
