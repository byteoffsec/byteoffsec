namespace ReClassNET.Nodes
{
	/// <summary>
	/// Global policy for how <see cref="BaseContainerNode"/> reacts when a child node changes its size
	/// (type change, text length, array count, bit field size, wrapped node change).
	/// </summary>
	public static class ContainerLayoutPolicy
	{
		/// <summary>
		/// If true (default) a container keeps the offsets of all nodes following a resized node stable:
		/// a node that grows consumes the bytes of its successors and a node that shrinks is padded with hex nodes.
		/// If false the legacy ReClass.NET behaviour is used where a growing node pushes every following node down.
		/// </summary>
		public static bool PreserveSuccessorOffsets { get; set; } = true;
	}
}
