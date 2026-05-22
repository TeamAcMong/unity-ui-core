namespace DreamTech.UICore.Animations.Sequence
{
    /// <summary>
    /// Controls how steps inside an <see cref="AnimationSequence"/> are executed.
    /// </summary>
    public enum AnimationSequenceMode
    {
        /// <summary>
        /// Steps run one after another. Each step must complete before the next begins.
        /// </summary>
        Sequential,

        /// <summary>
        /// All steps start simultaneously. The sequence completes when every step finishes.
        /// </summary>
        Parallel,
    }
}
