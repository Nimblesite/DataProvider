# AI Work Scheduler Specification

## Overview

**AIWorkScheduler** is an intelligent, distributed job scheduling framework that combines traditional task queue mechanics (like Hangfire) with modern AI-driven optimization. The scheduler dynamically adjusts job priorities, reorders queues, and makes intelligent decisions based on real-time system state, historical performance, and predicted outcomes.

### Design Goals

1. **Self-Optimizing** – AI continuously learns from execution patterns to improve scheduling decisions
2. **Context-Aware** – Jobs carry rich context that AI uses for intelligent prioritization
3. **Deadline-Driven** – First-class support for SLAs and deadline constraints
4. **Resource-Aware** – Understands worker capabilities and resource costs
5. **Fault-Tolerant** – Graceful degradation, automatic retry with learned backoff
6. **Observable** – Rich telemetry for AI training and human debugging

---

## Research Foundation

This design incorporates cutting-edge scheduling research:

| Research Area | Key Concepts | References |
|--------------|--------------|------------|
| **Reinforcement Learning for Scheduling** | Q-learning, Deep Q-Networks for dynamic priority adjustment | [DeepRM: Resource Management with Deep RL](https://people.csail.mit.edu/alizadeh/papers/deeprm-hotnets16.pdf) |
| **Multi-Objective Optimization** | Pareto-optimal scheduling considering latency, throughput, cost | [MOSAIC: Multi-Objective Scheduling](https://dl.acm.org/doi/10.1145/3297858.3304071) |
| **Decima (MIT/Microsoft)** | Graph Neural Networks for cluster scheduling | [Decima: Learning Scheduling Algorithms](https://web.mit.edu/decima/) |
| **Deadline-Aware Scheduling** | Earliest Deadline First (EDF) with slack management | [Real-Time Systems - Liu & Layland](https://dl.acm.org/doi/10.1145/321738.321743) |
| **Predictive Autoscaling** | LSTM/Transformer models for workload prediction | [Autopilot: Workload Autoscaling at Google](https://research.google/pubs/pub49174/) |
| **Queueing Theory** | M/G/1, Priority queues, Little's Law for capacity planning | [Performance Modeling - Mor Harchol-Balter](https://www.cs.cmu.edu/~harchol/PerformanceModeling/book.html) |
| **Chaos Engineering for Schedulers** | Fault injection to train resilient scheduling policies | [Netflix Chaos Engineering](https://netflixtechblog.com/chaos-engineering-upgraded-878d341f15fa) |

---

## Core Concepts

### Job

A unit of work with rich metadata for AI decision-making.

```csharp
/// <summary>Immutable job definition with AI context</summary>
public sealed record Job(
    Guid Id,
    string Queue,
    string TypeName,
    string SerializedArgs,
    JobPriority BasePriority,
    DateTimeOffset CreatedAt,
    DateTimeOffset? Deadline,
    TimeSpan? ExpectedDuration,
    ImmutableDictionary<string, string> Context,
    Guid? ParentJobId,
    Guid? CorrelationId,
    int MaxRetries,
    string? IdempotencyKey
);

/// <summary>Priority levels with numeric weight for AI interpolation</summary>
public enum JobPriority
{
    Background = 0,
    Low = 25,
    Normal = 50,
    High = 75,
    Critical = 100
}
```

### Job Context

The `Context` dictionary carries domain-specific metadata that AI uses for intelligent scheduling:

| Key | Purpose | Example |
|-----|---------|---------|
| `tenant_id` | Fair scheduling across tenants | `"tenant_123"` |
| `user_tier` | Premium users get priority boost | `"enterprise"` |
| `cost_center` | Resource accounting | `"billing"` |
| `data_locality` | Prefer workers near data | `"region:us-east"` |
| `resource_hint` | Expected resource usage | `"cpu:high,memory:2gb"` |
| `dependency_chain` | Critical path identification | `"checkout_flow"` |
| `sla_tier` | SLA classification | `"p99_100ms"` |

### Effective Priority

AI computes an **effective priority** at scheduling time, considering:

```
EffectivePriority = BasePriority
    + DeadlineUrgency(deadline, now)
    + TenantFairness(tenant, recentUsage)
    + ResourceAvailability(hint, workerState)
    + LearnedAdjustment(jobType, historicalPerformance)
    + DependencyBoost(waitingDownstreamJobs)
```

---

## AI Scheduling Engine

### Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                        AI Scheduler Core                         │
├─────────────────────────────────────────────────────────────────┤
│  ┌───────────────┐  ┌───────────────┐  ┌───────────────────┐   │
│  │ Priority      │  │ Workload      │  │ Anomaly           │   │
│  │ Optimizer     │  │ Predictor     │  │ Detector          │   │
│  │ (DQN/PPO)     │  │ (LSTM)        │  │ (Isolation Forest)│   │
│  └───────┬───────┘  └───────┬───────┘  └─────────┬─────────┘   │
│          │                  │                    │              │
│          └──────────────────┼────────────────────┘              │
│                             ▼                                    │
│                   ┌─────────────────┐                           │
│                   │ Decision Engine │                           │
│                   │ (Rule + ML)     │                           │
│                   └────────┬────────┘                           │
│                            │                                     │
├────────────────────────────┼────────────────────────────────────┤
│                            ▼                                     │
│  ┌─────────┐  ┌─────────┐  ┌─────────┐  ┌─────────┐            │
│  │ Queue 1 │  │ Queue 2 │  │ Queue 3 │  │ Queue N │            │
│  └────┬────┘  └────┬────┘  └────┬────┘  └────┬────┘            │
│       │            │            │            │                   │
│       └────────────┴────────────┴────────────┘                   │
│                            │                                     │
│                            ▼                                     │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐        │
│  │ Worker 1 │  │ Worker 2 │  │ Worker 3 │  │ Worker N │        │
│  │ cpu:4    │  │ cpu:8    │  │ gpu:1    │  │ cpu:2    │        │
│  └──────────┘  └──────────┘  └──────────┘  └──────────┘        │
└─────────────────────────────────────────────────────────────────┘
```

### Priority Optimizer (Reinforcement Learning)

Uses **Proximal Policy Optimization (PPO)** to learn optimal priority adjustments.

**State Space:**
- Queue depths per priority level
- Worker utilization percentages
- Jobs approaching deadline (sliding window)
- Historical throughput (last 1h, 24h, 7d)
- Current time-of-day/day-of-week

**Action Space:**
- Priority boost/reduction per job type (-20 to +20)
- Queue reordering recommendations
- Worker affinity assignments

**Reward Function:**
```
Reward = Σ(completed_jobs × priority_weight)
       - Σ(deadline_misses × deadline_penalty)
       - Σ(fairness_violation × fairness_penalty)
       - resource_cost × cost_weight
```

**Training:** Continuous online learning with experience replay buffer stored in `scheduler_ai_experience` table.

### Workload Predictor (Time Series)

LSTM-based model predicting job arrival rates and processing times.

**Inputs:**
- Historical arrival patterns (hourly/daily/weekly seasonality)
- Current pending job count
- External signals (deployment events, marketing campaigns)

**Outputs:**
- Predicted arrivals next 1h/6h/24h
- Recommended worker scaling

### Anomaly Detector

Identifies scheduling anomalies that require human intervention:

- Jobs stuck in processing (zombies)
- Sudden throughput drops
- Unusual failure rate spikes
- Resource exhaustion patterns

---

## Database Schema (YAML Migration)

```yaml
name: scheduler
tables:
  # Core job storage
  - name: scheduler_job
    columns:
      - name: Id
        type: Text
      - name: Queue
        type: Text
      - name: TypeName
        type: Text
      - name: SerializedArgs
        type: Text
      - name: BasePriority
        type: Int
      - name: EffectivePriority
        type: Int
      - name: State
        type: Text
        checkConstraint: State IN ('pending', 'scheduled', 'processing', 'completed', 'failed', 'dead')
      - name: CreatedAt
        type: Text
      - name: ScheduledAt
        type: Text
      - name: StartedAt
        type: Text
      - name: CompletedAt
        type: Text
      - name: Deadline
        type: Text
      - name: ExpectedDurationMs
        type: Int
      - name: ActualDurationMs
        type: Int
      - name: WorkerId
        type: Text
      - name: RetryCount
        type: Int
        defaultValue: 0
      - name: MaxRetries
        type: Int
        defaultValue: 3
      - name: ParentJobId
        type: Text
      - name: CorrelationId
        type: Text
      - name: IdempotencyKey
        type: Text
      - name: ErrorMessage
        type: Text
      - name: ErrorStackTrace
        type: Text
      - name: ContextJson
        type: Text
      - name: ResultJson
        type: Text
      - name: Version
        type: Int
        defaultValue: 1
    primaryKey:
      name: PK_scheduler_job
      columns:
        - Id
    indexes:
      - name: idx_job_queue_state_priority
        columns:
          - Queue
          - State
          - EffectivePriority
      - name: idx_job_state_scheduled
        columns:
          - State
          - ScheduledAt
      - name: idx_job_correlation
        columns:
          - CorrelationId
      - name: idx_job_idempotency
        columns:
          - IdempotencyKey
        isUnique: true
      - name: idx_job_deadline
        columns:
          - Deadline
          - State

  # Worker registration and heartbeat
  - name: scheduler_worker
    columns:
      - name: Id
        type: Text
      - name: Hostname
        type: Text
      - name: ProcessId
        type: Int
      - name: Queues
        type: Text
      - name: Concurrency
        type: Int
      - name: ResourceTags
        type: Text
      - name: State
        type: Text
        checkConstraint: State IN ('active', 'draining', 'stopped')
      - name: LastHeartbeatAt
        type: Text
      - name: StartedAt
        type: Text
      - name: ProcessedCount
        type: Int
        defaultValue: 0
      - name: FailedCount
        type: Int
        defaultValue: 0
      - name: CurrentJobIds
        type: Text
    primaryKey:
      name: PK_scheduler_worker
      columns:
        - Id
    indexes:
      - name: idx_worker_state_heartbeat
        columns:
          - State
          - LastHeartbeatAt

  # Job context key-value pairs (denormalized for query performance)
  - name: scheduler_job_context
    columns:
      - name: Id
        type: Text
      - name: JobId
        type: Text
      - name: Key
        type: Text
      - name: Value
        type: Text
    primaryKey:
      name: PK_scheduler_job_context
      columns:
        - Id
    foreignKeys:
      - name: FK_job_context_job
        columns:
          - JobId
        referencedTable: scheduler_job
        referencedColumns:
          - Id
        onDelete: Cascade
    indexes:
      - name: idx_context_job
        columns:
          - JobId
      - name: idx_context_key_value
        columns:
          - Key
          - Value

  # Recurring job definitions
  - name: scheduler_recurring
    columns:
      - name: Id
        type: Text
      - name: Name
        type: Text
      - name: CronExpression
        type: Text
      - name: TimeZone
        type: Text
        defaultValue: "'UTC'"
      - name: Queue
        type: Text
      - name: TypeName
        type: Text
      - name: SerializedArgs
        type: Text
      - name: BasePriority
        type: Int
      - name: ContextJson
        type: Text
      - name: LastEnqueuedAt
        type: Text
      - name: NextEnqueueAt
        type: Text
      - name: Enabled
        type: Int
        defaultValue: 1
    primaryKey:
      name: PK_scheduler_recurring
      columns:
        - Id
    indexes:
      - name: idx_recurring_next
        columns:
          - Enabled
          - NextEnqueueAt

  # AI training experience buffer
  - name: scheduler_ai_experience
    columns:
      - name: Id
        type: Text
      - name: Timestamp
        type: Text
      - name: StateJson
        type: Text
      - name: ActionJson
        type: Text
      - name: Reward
        type: Double
      - name: NextStateJson
        type: Text
      - name: Done
        type: Int
    primaryKey:
      name: PK_scheduler_ai_experience
      columns:
        - Id
    indexes:
      - name: idx_experience_timestamp
        columns:
          - Timestamp

  # AI model snapshots
  - name: scheduler_ai_model
    columns:
      - name: Id
        type: Text
      - name: ModelType
        type: Text
        checkConstraint: ModelType IN ('priority_optimizer', 'workload_predictor', 'anomaly_detector')
      - name: Version
        type: Int
      - name: TrainedAt
        type: Text
      - name: MetricsJson
        type: Text
      - name: WeightsBlob
        type: Blob
      - name: IsActive
        type: Int
        defaultValue: 0
    primaryKey:
      name: PK_scheduler_ai_model
      columns:
        - Id
    indexes:
      - name: idx_model_type_active
        columns:
          - ModelType
          - IsActive

  # Scheduling metrics for AI training
  - name: scheduler_metrics
    columns:
      - name: Id
        type: Text
      - name: Timestamp
        type: Text
      - name: MetricType
        type: Text
      - name: Queue
        type: Text
      - name: Value
        type: Double
      - name: DimensionsJson
        type: Text
    primaryKey:
      name: PK_scheduler_metrics
      columns:
        - Id
    indexes:
      - name: idx_metrics_type_time
        columns:
          - MetricType
          - Timestamp
      - name: idx_metrics_queue_time
        columns:
          - Queue
          - Timestamp

  # Dead letter queue with failure analysis
  - name: scheduler_dead_letter
    columns:
      - name: Id
        type: Text
      - name: OriginalJobId
        type: Text
      - name: Queue
        type: Text
      - name: TypeName
        type: Text
      - name: SerializedArgs
        type: Text
      - name: ContextJson
        type: Text
      - name: FailedAt
        type: Text
      - name: FailureReason
        type: Text
      - name: RetryCount
        type: Int
      - name: FailureCategory
        type: Text
        checkConstraint: FailureCategory IN ('transient', 'permanent', 'timeout', 'resource', 'unknown')
      - name: ReprocessedAt
        type: Text
    primaryKey:
      name: PK_scheduler_dead_letter
      columns:
        - Id
    indexes:
      - name: idx_dead_letter_category
        columns:
          - FailureCategory
          - FailedAt
```

---

## Record Types

### Core Types

```csharp
// GlobalUsings.cs
global using EnqueueResult = Outcome.Result<Guid, Scheduler.SchedulerError>;
global using EnqueueOk = Outcome.Result<Guid, Scheduler.SchedulerError>.Ok<Guid, Scheduler.SchedulerError>;
global using EnqueueError = Outcome.Result<Guid, Scheduler.SchedulerError>.Error<Guid, Scheduler.SchedulerError>;

global using DequeueResult = Outcome.Result<Scheduler.JobExecution, Scheduler.SchedulerError>;
global using CompleteResult = Outcome.Result<Scheduler.JobCompletion, Scheduler.SchedulerError>;
global using SchedulerVoidResult = Outcome.Result<Scheduler.Unit, Scheduler.SchedulerError>;
```

```csharp
namespace Scheduler;

/// <summary>Void result placeholder for operations with no return value</summary>
public sealed record Unit
{
    public static readonly Unit Value = new();
    private Unit() { }
}

/// <summary>Job state machine states</summary>
public enum JobState
{
    Pending,
    Scheduled,
    Processing,
    Completed,
    Failed,
    Dead
}

/// <summary>Failure categories for AI-driven retry decisions</summary>
public enum FailureCategory
{
    Transient,   // Retry immediately with backoff
    Permanent,   // Move to dead letter
    Timeout,     // Retry with longer timeout
    Resource,    // Retry when resources available
    Unknown      // Needs manual investigation
}

/// <summary>Base error type with closed hierarchy</summary>
public abstract record SchedulerError
{
    private protected SchedulerError() { }
}

public sealed record SchedulerErrorNotFound(string EntityType, Guid Id) : SchedulerError;
public sealed record SchedulerErrorConcurrency(Guid JobId, int ExpectedVersion, int ActualVersion) : SchedulerError;
public sealed record SchedulerErrorTimeout(Guid JobId, TimeSpan Elapsed) : SchedulerError;
public sealed record SchedulerErrorSerialization(string TypeName, string Details) : SchedulerError;
public sealed record SchedulerErrorDatabase(string Operation, string Details) : SchedulerError;
public sealed record SchedulerErrorWorkerUnavailable(string Queue) : SchedulerError;
public sealed record SchedulerErrorIdempotencyViolation(string Key, Guid ExistingJobId) : SchedulerError;

/// <summary>Immutable job definition</summary>
public sealed record Job(
    Guid Id,
    string Queue,
    string TypeName,
    string SerializedArgs,
    JobPriority BasePriority,
    int EffectivePriority,
    JobState State,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ScheduledAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? Deadline,
    TimeSpan? ExpectedDuration,
    TimeSpan? ActualDuration,
    Guid? WorkerId,
    int RetryCount,
    int MaxRetries,
    Guid? ParentJobId,
    Guid? CorrelationId,
    string? IdempotencyKey,
    string? ErrorMessage,
    ImmutableDictionary<string, string> Context,
    int Version
);

/// <summary>Job handed to worker for execution</summary>
public sealed record JobExecution(
    Guid JobId,
    string TypeName,
    string SerializedArgs,
    ImmutableDictionary<string, string> Context,
    DateTimeOffset? Deadline,
    int AttemptNumber,
    CancellationToken CancellationToken
);

/// <summary>Result of job completion</summary>
public sealed record JobCompletion(
    Guid JobId,
    bool Success,
    TimeSpan Duration,
    string? ResultJson,
    FailureCategory? FailureCategory,
    string? ErrorMessage
);

/// <summary>Worker registration</summary>
public sealed record Worker(
    Guid Id,
    string Hostname,
    int ProcessId,
    ImmutableArray<string> Queues,
    int Concurrency,
    ImmutableDictionary<string, string> ResourceTags,
    WorkerState State,
    DateTimeOffset LastHeartbeatAt,
    DateTimeOffset StartedAt,
    long ProcessedCount,
    long FailedCount,
    ImmutableArray<Guid> CurrentJobIds
);

public enum WorkerState
{
    Active,
    Draining,
    Stopped
}

/// <summary>Recurring job schedule</summary>
public sealed record RecurringJob(
    Guid Id,
    string Name,
    string CronExpression,
    string TimeZone,
    string Queue,
    string TypeName,
    string SerializedArgs,
    JobPriority BasePriority,
    ImmutableDictionary<string, string> Context,
    DateTimeOffset? LastEnqueuedAt,
    DateTimeOffset NextEnqueueAt,
    bool Enabled
);
```

### AI Types

```csharp
namespace Scheduler.AI;

/// <summary>Scheduler state snapshot for RL</summary>
public sealed record SchedulerState(
    ImmutableDictionary<string, QueueState> Queues,
    ImmutableArray<WorkerSnapshot> Workers,
    DateTimeOffset Timestamp,
    DayOfWeek DayOfWeek,
    int HourOfDay
);

/// <summary>Queue state for AI observation</summary>
public sealed record QueueState(
    string Name,
    int PendingCount,
    int ProcessingCount,
    int DeadlineApproachingCount,
    double AvgWaitTimeMs,
    double P99WaitTimeMs,
    ImmutableDictionary<JobPriority, int> CountByPriority
);

/// <summary>Worker snapshot for AI observation</summary>
public sealed record WorkerSnapshot(
    Guid Id,
    double CpuUtilization,
    double MemoryUtilization,
    int ActiveJobCount,
    int Concurrency,
    ImmutableDictionary<string, string> ResourceTags
);

/// <summary>AI scheduling decision</summary>
public sealed record SchedulingDecision(
    Guid JobId,
    int PriorityAdjustment,
    Guid? PreferredWorkerId,
    string Reasoning
);

/// <summary>Experience tuple for RL training</summary>
public sealed record Experience(
    Guid Id,
    DateTimeOffset Timestamp,
    SchedulerState State,
    SchedulingAction Action,
    double Reward,
    SchedulerState? NextState,
    bool Done
);

/// <summary>Action taken by AI scheduler</summary>
public sealed record SchedulingAction(
    ImmutableArray<SchedulingDecision> Decisions,
    ImmutableDictionary<string, int> QueuePriorityBoosts
);

/// <summary>AI model metadata</summary>
public sealed record AIModel(
    Guid Id,
    AIModelType Type,
    int Version,
    DateTimeOffset TrainedAt,
    ImmutableDictionary<string, double> Metrics,
    byte[] Weights,
    bool IsActive
);

public enum AIModelType
{
    PriorityOptimizer,
    WorkloadPredictor,
    AnomalyDetector
}
```

---

## API Surface

### Enqueue Operations

```csharp
public static class Scheduler
{
    /// <summary>Enqueue a job for immediate processing</summary>
    public static EnqueueResult Enqueue<TJob>(
        Func<SqliteConnection> getConnection,
        TJob job,
        string queue = "default",
        JobPriority priority = JobPriority.Normal,
        ImmutableDictionary<string, string>? context = null,
        string? idempotencyKey = null,
        ILogger? logger = null
    ) where TJob : notnull;

    /// <summary>Enqueue a job for future processing</summary>
    public static EnqueueResult Schedule<TJob>(
        Func<SqliteConnection> getConnection,
        TJob job,
        DateTimeOffset scheduledAt,
        string queue = "default",
        JobPriority priority = JobPriority.Normal,
        DateTimeOffset? deadline = null,
        ImmutableDictionary<string, string>? context = null,
        ILogger? logger = null
    ) where TJob : notnull;

    /// <summary>Create or update a recurring job</summary>
    public static SchedulerVoidResult AddOrUpdateRecurring<TJob>(
        Func<SqliteConnection> getConnection,
        string recurringJobId,
        TJob job,
        string cronExpression,
        string queue = "default",
        JobPriority priority = JobPriority.Normal,
        string timeZone = "UTC",
        ImmutableDictionary<string, string>? context = null,
        ILogger? logger = null
    ) where TJob : notnull;

    /// <summary>Enqueue a continuation job</summary>
    public static EnqueueResult ContinueWith<TJob>(
        Func<SqliteConnection> getConnection,
        Guid parentJobId,
        TJob job,
        string queue = "default",
        JobPriority priority = JobPriority.Normal,
        ILogger? logger = null
    ) where TJob : notnull;
}
```

### Worker Operations

```csharp
public static class SchedulerWorker
{
    /// <summary>Register a worker and begin processing</summary>
    public static async Task RunAsync(
        Func<SqliteConnection> getConnection,
        ImmutableArray<string> queues,
        int concurrency,
        Func<JobExecution, Task<JobCompletion>> processJob,
        ImmutableDictionary<string, string>? resourceTags = null,
        CancellationToken cancellationToken = default,
        ILogger? logger = null
    );

    /// <summary>Signal worker to drain and stop</summary>
    public static SchedulerVoidResult RequestDrain(
        Func<SqliteConnection> getConnection,
        Guid workerId,
        ILogger? logger = null
    );
}
```

### AI Operations

```csharp
public static class SchedulerAI
{
    /// <summary>Run AI priority optimization pass</summary>
    public static async Task<SchedulerVoidResult> OptimizePrioritiesAsync(
        Func<SqliteConnection> getConnection,
        AIModel model,
        ILogger? logger = null
    );

    /// <summary>Get workload prediction for capacity planning</summary>
    public static async Task<Outcome.Result<WorkloadPrediction, SchedulerError>> PredictWorkloadAsync(
        Func<SqliteConnection> getConnection,
        AIModel model,
        TimeSpan horizon,
        ILogger? logger = null
    );

    /// <summary>Train model on recent experiences</summary>
    public static async Task<Outcome.Result<AIModel, SchedulerError>> TrainModelAsync(
        Func<SqliteConnection> getConnection,
        AIModelType modelType,
        TrainingConfig config,
        ILogger? logger = null
    );

    /// <summary>Detect scheduling anomalies</summary>
    public static async Task<Outcome.Result<ImmutableArray<Anomaly>, SchedulerError>> DetectAnomaliesAsync(
        Func<SqliteConnection> getConnection,
        AIModel model,
        ILogger? logger = null
    );
}

public sealed record WorkloadPrediction(
    ImmutableDictionary<string, ImmutableArray<PredictedLoad>> ByQueue,
    int RecommendedWorkerCount,
    double ConfidenceScore
);

public sealed record PredictedLoad(
    DateTimeOffset Timestamp,
    double ExpectedArrivalRate,
    double LowerBound,
    double UpperBound
);

public sealed record Anomaly(
    AnomalyType Type,
    string Description,
    double Severity,
    ImmutableDictionary<string, string> Context
);

public enum AnomalyType
{
    ZombieJob,
    ThroughputDrop,
    FailureSpike,
    ResourceExhaustion,
    DeadlineMissPattern
}

public sealed record TrainingConfig(
    int BatchSize,
    int Epochs,
    double LearningRate,
    int ExperienceBufferSize
);
```

---

## Integration Pattern

### Minimal Setup

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Register connection factory (no singletons)
builder.Services.AddSingleton<Func<SqliteConnection>>(() =>
{
    var conn = new SqliteConnection(builder.Configuration.GetConnectionString("Scheduler"));
    conn.Open();
    return conn;
});

// Register background worker
builder.Services.AddHostedService<SchedulerWorkerService>();

var app = builder.Build();

// Enqueue jobs from API endpoints
app.MapPost("/orders", async (CreateOrderRequest request, Func<SqliteConnection> getConn) =>
{
    var result = Scheduler.Enqueue(
        getConnection: getConn,
        job: new ProcessOrderJob(request.OrderId),
        queue: "orders",
        priority: JobPriority.High,
        context: ImmutableDictionary<string, string>.Empty
            .Add("tenant_id", request.TenantId)
            .Add("user_tier", request.UserTier)
    );

    return result switch
    {
        EnqueueOk ok => Results.Accepted($"/jobs/{ok.Value}"),
        EnqueueError err => Results.Problem(err.Value.ToString())
    };
});
```

### Worker Service

```csharp
internal sealed class SchedulerWorkerService : BackgroundService
{
    private readonly ILogger<SchedulerWorkerService> _logger;
    private readonly Func<SqliteConnection> _getConnection;
    private readonly IServiceProvider _services;

    public SchedulerWorkerService(
        ILogger<SchedulerWorkerService> logger,
        Func<SqliteConnection> getConnection,
        IServiceProvider services
    )
    {
        _logger = logger;
        _getConnection = getConnection;
        _services = services;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await SchedulerWorker.RunAsync(
            getConnection: _getConnection,
            queues: ["orders", "notifications", "reports"],
            concurrency: Environment.ProcessorCount,
            processJob: ProcessJobAsync,
            resourceTags: ImmutableDictionary<string, string>.Empty
                .Add("region", "us-east-1")
                .Add("instance_type", "c5.xlarge"),
            cancellationToken: stoppingToken,
            logger: _logger
        ).ConfigureAwait(false);
    }

    private async Task<JobCompletion> ProcessJobAsync(JobExecution execution)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Resolve and execute job handler
            var jobType = Type.GetType(execution.TypeName);
            var handler = _services.GetRequiredService(typeof(IJobHandler<>).MakeGenericType(jobType!));

            await ((dynamic)handler).HandleAsync(
                JsonSerializer.Deserialize(execution.SerializedArgs, jobType),
                execution.Context,
                execution.CancellationToken
            ).ConfigureAwait(false);

            return new JobCompletion(
                JobId: execution.JobId,
                Success: true,
                Duration: stopwatch.Elapsed,
                ResultJson: null,
                FailureCategory: null,
                ErrorMessage: null
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Job {JobId} failed: {Message}", execution.JobId, ex.Message);

            return new JobCompletion(
                JobId: execution.JobId,
                Success: false,
                Duration: stopwatch.Elapsed,
                ResultJson: null,
                FailureCategory: ClassifyFailure(ex),
                ErrorMessage: ex.Message
            );
        }
    }

    private static FailureCategory ClassifyFailure(Exception ex) => ex switch
    {
        TimeoutException => FailureCategory.Timeout,
        HttpRequestException { StatusCode: >= (System.Net.HttpStatusCode)500 } => FailureCategory.Transient,
        HttpRequestException { StatusCode: >= (System.Net.HttpStatusCode)400 } => FailureCategory.Permanent,
        OutOfMemoryException => FailureCategory.Resource,
        InvalidOperationException => FailureCategory.Permanent,
        _ => FailureCategory.Unknown
    };
}
```

---

## AI Training Loop

```csharp
internal sealed class AITrainingWorker : BackgroundService
{
    private readonly ILogger<AITrainingWorker> _logger;
    private readonly Func<SqliteConnection> _getConnection;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Collect experiences from recent scheduling decisions
            var state = await CaptureSchedulerStateAsync().ConfigureAwait(false);

            // Run priority optimization with current model
            var modelResult = await GetActiveModelAsync(AIModelType.PriorityOptimizer).ConfigureAwait(false);

            if (modelResult is Outcome.Result<AIModel, SchedulerError>.Ok<AIModel, SchedulerError> ok)
            {
                await SchedulerAI.OptimizePrioritiesAsync(
                    getConnection: _getConnection,
                    model: ok.Value,
                    logger: _logger
                ).ConfigureAwait(false);
            }

            // Periodically retrain models
            if (ShouldRetrain())
            {
                _logger.LogInformation("Starting AI model retraining");

                await SchedulerAI.TrainModelAsync(
                    getConnection: _getConnection,
                    modelType: AIModelType.PriorityOptimizer,
                    config: new TrainingConfig(
                        BatchSize: 64,
                        Epochs: 10,
                        LearningRate: 0.001,
                        ExperienceBufferSize: 10000
                    ),
                    logger: _logger
                ).ConfigureAwait(false);
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken).ConfigureAwait(false);
        }
    }
}
```

---

## Observability

### Metrics Emitted

| Metric | Type | Labels | Description |
|--------|------|--------|-------------|
| `scheduler_jobs_enqueued_total` | Counter | queue, priority | Jobs enqueued |
| `scheduler_jobs_completed_total` | Counter | queue, status | Jobs completed |
| `scheduler_jobs_duration_seconds` | Histogram | queue, job_type | Processing time |
| `scheduler_queue_depth` | Gauge | queue, priority | Pending jobs |
| `scheduler_deadline_misses_total` | Counter | queue | SLA violations |
| `scheduler_ai_priority_adjustments` | Histogram | queue | AI adjustments |
| `scheduler_worker_utilization` | Gauge | worker_id | Worker busyness |

### Structured Logging

```csharp
_logger.LogInformation(
    "Job {JobId} scheduled with effective priority {EffectivePriority} " +
    "(base: {BasePriority}, AI adjustment: {Adjustment})",
    job.Id,
    effectivePriority,
    job.BasePriority,
    aiAdjustment
);
```

---

## Future Enhancements

1. **Federated Learning** – Train models across microservices without centralizing data
2. **Explainable AI** – Human-readable explanations for scheduling decisions
3. **Predictive SLA Alerts** – Warn before deadlines are missed
4. **Cost-Aware Scheduling** – Optimize for cloud compute costs
5. **A/B Testing** – Compare scheduling strategies in production

---

## References

- [Decima: Learning to Schedule](https://web.mit.edu/decima/) - MIT/Microsoft graph neural network scheduler
- [DeepRM: Resource Management with Deep RL](https://people.csail.mit.edu/alizadeh/papers/deeprm-hotnets16.pdf) - MIT CSAIL
- [Autopilot: Google's Workload Autoscaling](https://research.google/pubs/pub49174/) - Google Research
- [Proximal Policy Optimization (PPO)](https://arxiv.org/abs/1707.06347) - OpenAI
- [Isolation Forest for Anomaly Detection](https://cs.nju.edu.cn/zhouzh/zhouzh.files/publication/icdm08b.pdf) - Zhou et al.
- [Performance Modeling and Design of Computer Systems](https://www.cs.cmu.edu/~harchol/PerformanceModeling/book.html) - CMU
- [Real-Time Systems (Liu & Layland)](https://dl.acm.org/doi/10.1145/321738.321743) - Foundational EDF paper
- [Hangfire Documentation](https://docs.hangfire.io/) - Inspiration for API design
