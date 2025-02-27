using k8s;
using k8s.Models;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace CRD.Models
{
    public class V1DailyTask : IKubernetesObject<V1ObjectMeta>
    {
        [JsonPropertyName("apiVersion")]
        public string ApiVersion { get; set; } = "example.com/v1"; // Aligned with CRD yaml

        [JsonPropertyName("kind")]
        public string Kind { get; set; } = "DailyTask";

        [JsonPropertyName("metadata")]
        public V1ObjectMeta Metadata { get; set; } = new V1ObjectMeta();

        [JsonPropertyName("spec")]
        public V1DailyTaskSpec Spec { get; set; } = new V1DailyTaskSpec();

        [JsonPropertyName("status")]
        public V1DailyTaskStatus? Status { get; set; }
    }

    public class V1DailyTaskStatus
    {
        [JsonPropertyName("today")]
        public DayInfo Today { get; set; }
    }

    public class DayInfo
    {
        [JsonPropertyName("time")]
        public string Time { get; set; }

        [JsonPropertyName("day")]
        public string Day { get; set; }

        
        public enum Rating
        {
            Good,
            Bad
        }

        [JsonPropertyName("rating")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public Rating rating { get; set; }
    }

    public class V1DailyTaskSpec
    {
        [JsonPropertyName("taskName")]
        public string TaskName { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("priority")]
        public int Priority { get; set; }
    }
}
