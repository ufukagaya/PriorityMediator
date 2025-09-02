// Extensions/PriorityFlow/Priority.cs
// Bu dosyayı Visual Studio'da Extensions/PriorityFlow klasörüne oluştur

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MediatR;

namespace JetRentalOrchestration.Extensions.PriorityFlow
{
    /// <summary>
    /// Command execution priority levels
    /// Yüksek sayı = Yüksek priority (Critical = 10, Background = 1)
    /// </summary>
    public enum Priority
    {
        Background = 1,  // En düşük priority - sistem boştayken çalışır
        Low = 3,         // Düşük priority - normal işlemlerden sonra
        Normal = 5,      // Normal priority - default değer
        High = 7,        // Yüksek priority - önemli işlemler
        Critical = 10    // En yüksek priority - acil işlemler
    }

    /// <summary>
    /// Command class'larına priority vermek için attribute
    /// Kullanım: [Priority(Priority.High)]
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class PriorityAttribute : Attribute
    {
        public Priority Priority { get; }

        public PriorityAttribute(Priority priority)
        {
            Priority = priority;
        }
    }

    /// <summary>
    /// Auto-orchestration için interface
    /// Command execute edildikten sonra hangi command'ların çalışacağını belirler
    /// </summary>
    public interface IWorkflowCommand<T> : IRequest<T>
    {
        /// <summary>
        /// Ana command çalıştıktan sonra otomatik olarak çalıştırılacak command'ları return et
        /// Bu method'un return ettiği command'lar priority sırasıyla otomatik execute edilir
        /// </summary>
        /// <param name="result">Ana command'ın result'ı</param>
        /// <returns>Otomatik çalıştırılacak command'lar</returns>
        IEnumerable<IBaseRequest> GetFollowUpCommands(T result);
    }

    /// <summary>
    /// Priority queue için item wrapper
    /// Her queue item'ı command + priority + timing info taşır
    /// </summary>
    public class PriorityQueueItem
    {
        public IBaseRequest Command { get; set; } = null!; // IBaseRequest hem IRequest hem IRequest<T>'yi kapsar
        public Priority Priority { get; set; }
        public DateTime QueuedAt { get; set; }
        public TaskCompletionSource<object> CompletionSource { get; set; } = null!;
        public string CommandType => Command.GetType().Name;
    }
}