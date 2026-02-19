---
layout: default
title: Alerting API
parent: API Reference
nav_order: 6
has_children: true
---

# Alerting API Reference

Complete reference for the ChainSharp.Effect.Provider.Alerting extension.

## Overview

The Alerting API provides metric-based alerting for workflow failures with customizable conditions and multiple alert destinations.

## Interfaces

- **[IAlertingWorkflow&lt;TIn, TOut&gt;]({% link api-reference/alerting-api/i-alerting-workflow.md %})** - Workflow interface for enabling alerting
- **[IAlertSender]({% link api-reference/alerting-api/i-alert-sender.md %})** - Interface for implementing alert destinations
- **[IAlertConfigurationRegistry]({% link api-reference/alerting-api/i-alert-configuration-registry.md %})** - Registry for cached workflow alert configurations

## Configuration Classes

- **[AlertConfigurationBuilder]({% link api-reference/alerting-api/alert-configuration-builder.md %})** - Fluent builder for alert conditions
- **[AlertConfiguration]({% link api-reference/alerting-api/alert-configuration.md %})** - Result of AlertConfigurationBuilder.Build()
- **[AlertingOptionsBuilder]({% link api-reference/alerting-api/alerting-options-builder.md %})** - Builder for configuring alert senders and debouncing

## Context Classes

- **[AlertContext]({% link api-reference/alerting-api/alert-context.md %})** - Comprehensive context passed to IAlertSender implementations

## Service Registration

- **[UseAlertingEffect]({% link api-reference/alerting-api/use-alerting-effect.md %})** - Extension method for registering the alerting effect

## Analyzers

- **[ALERT001]({% link api-reference/alerting-api/alert001.md %})** - AlertConfiguration requires TimeWindow and MinimumFailures
- **[ALERT002]({% link api-reference/alerting-api/alert002.md %})** - UseAlertingEffect requires at least one alert sender

## Quick Links

- [Usage Guide]({% link usage-guide/alerting.md %})
- [Effect Providers]({% link api-reference/configuration.md %})
- [Metadata Reference]({% link concepts/metadata.md %})
