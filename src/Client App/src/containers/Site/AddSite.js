import React from "react";
import { Modal } from "react-bootstrap";
import SiteForm from "./SiteForm";

const AddSite = ({ show, onClose, pageNumber }) => {
	return (
		<Modal show={show} onHide={onClose} centered size="lg">
			<Modal.Body className="bgGradient">
				{<SiteForm onClose={onClose} pageNumber={pageNumber} />}
			</Modal.Body>
		</Modal>
	);
};

export default AddSite;
