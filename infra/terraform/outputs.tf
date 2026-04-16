output "ecr_repository_name" {
  description = "Amazon ECR repository name."
  value       = aws_ecr_repository.inventory_api.name
}

output "ecr_repository_url" {
  description = "Amazon ECR repository URL."
  value       = aws_ecr_repository.inventory_api.repository_url
}

output "eks_cluster_role_arn" {
  description = "IAM role ARN for the EKS control plane."
  value       = aws_iam_role.eks_cluster.arn
}

output "eks_node_group_role_arn" {
  description = "IAM role ARN for the EKS worker nodes."
  value       = aws_iam_role.eks_node_group.arn
}

output "ebs_csi_driver_role_arn" {
  description = "IAM role ARN used by the EBS CSI driver."
  value       = aws_iam_role.ebs_csi_driver.arn
}

output "vpc_id" {
  description = "VPC ID for the EKS network."
  value       = aws_vpc.main.id
}

output "public_subnet_ids" {
  description = "Public subnet IDs for the VPC."
  value       = aws_subnet.public[*].id
}

output "private_subnet_ids" {
  description = "Private subnet IDs for the VPC."
  value       = aws_subnet.private[*].id
}

output "eks_cluster_name" {
  description = "EKS cluster name."
  value       = aws_eks_cluster.main.name
}

output "eks_cluster_arn" {
  description = "EKS cluster ARN."
  value       = aws_eks_cluster.main.arn
}

output "eks_cluster_endpoint" {
  description = "EKS cluster API server endpoint."
  value       = aws_eks_cluster.main.endpoint
}

output "eks_node_group_name" {
  description = "EKS managed node group name."
  value       = aws_eks_node_group.main.node_group_name
}

output "eks_addon_names" {
  description = "EKS managed addons enabled for the cluster."
  value = [
    aws_eks_addon.vpc_cni.addon_name,
    aws_eks_addon.kube_proxy.addon_name,
    aws_eks_addon.coredns.addon_name,
    aws_eks_addon.pod_identity_agent.addon_name,
    aws_eks_addon.aws_ebs_csi_driver.addon_name
  ]
}
