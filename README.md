# ACMGT Learning prototype 1 & 2
## Interactive voxelization
The voxelizer monobehaviour makes use of compute shaders to enable interactive voxelization of the assigned model in order to accelerate ray tracing and enable voxel cone tracing.
## Animated octree ray casting
The caster monobehaviour makes use of transform matrices to allow dynamic movement and reorientation of the octree whilst casting rays into it. Implementation is based on the paper by Engmark Espe et al.[1].

## Preview
A preview of the project can be found [here](https://youtu.be/ZBHYKZ7gA9s).

## References
[1] Asbjørn Engmark Espe, Øystein Gjermundnes, and Sverre Hendseth. 2019. Efficient Animation of Sparse Voxel Octrees for Real-Time Ray Tracing.arXiv(2019),arXiv–1911.
