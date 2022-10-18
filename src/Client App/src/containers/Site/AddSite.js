import React from "react";
import { Modal } from "react-bootstrap";
import SiteForm from "./SiteForm";

const AddSite = ({ show, onClose, pageNumber }) => {
	return (
		<Modal show={show} onHide={onClose} centered size="lg">
			<Modal.Header closeButton>
				<Modal.Title>Add Site</Modal.Title>
			</Modal.Header>
				{<SiteForm onClose={onClose} pageNumber={pageNumber} />}
			
		</Modal>
	);
};

export default AddSite;
